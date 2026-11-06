/**
 * Session connection helper: the real-time half of a group visit.
 *
 * The server treats this connection as broadcast-only: every change goes through
 * the REST endpoints in `src/api.js` and the server sends the result to the room.
 * So this module only connects, joins a group and listens. If you want to send
 * something that changes state, add a REST call instead: the ownership and bounds
 * checks live there.
 */

import { HubConnectionBuilder } from '@microsoft/signalr';
import { getToken } from './api.js';

/** Activity actions worth showing the teacher, labelled as the student saw them. */
export const ACTIVITY_LABELS = {
  joined: 'activities.actions.joined',
  left: 'activities.actions.left',
  simpler: 'activities.actions.simpler',
  map: 'activities.actions.map',
};

/**
 * Connect and join the group for `code`.
 *
 * `handlers` is a map of event name → callback; every key is wired before the
 * join, so the catch-up `session:state` can't arrive before its listener exists.
 * Returns a disconnect function for the effect cleanup.
 */
export function connectSession(code, handlers = {}) {
  const connection = new HubConnectionBuilder()
    .withUrl('/hubs/session', { accessTokenFactory: () => getToken() })
    .withAutomaticReconnect()
    .build();

  for (const [event, handler] of Object.entries(handlers)) {
    connection.on(event, handler);
  }

  const join = () => connection.invoke('JoinSession', code);

  // Join again after every reconnect: group membership does not survive a dropped connection.
  connection.onreconnected(join);
  connection.start().then(join).catch(() => handlers['session:error']?.('Connection failed'));

  return () => connection.stop();
}
