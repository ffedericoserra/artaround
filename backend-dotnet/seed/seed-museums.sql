-- Dev seed: loads one data/museums/*.json document, passed as the psql variable :json.
-- Re-running replaces the museum: deleting it cascades to floor plans, POIs and contents.
-- Run through seed.sh, after `dotnet ef database update`.

\set ON_ERROR_STOP on

BEGIN;

CREATE TEMP TABLE doc ON COMMIT DROP AS SELECT :'json'::jsonb AS j;

-- Same rule as Node's generateSlug (the seed names have no accents).
CREATE TEMP TABLE museum_src ON COMMIT DROP AS
SELECT j -> 'museum' AS m,
       regexp_replace(regexp_replace(regexp_replace(lower(j -> 'museum' ->> 'name'),
           '[^a-z0-9\s-]', '', 'g'), '\s+', '-', 'g'), '-+', '-', 'g') AS slug
FROM doc;

DELETE FROM museums WHERE slug = (SELECT slug FROM museum_src);

INSERT INTO museums (name, slug, address, description, website, email, phone, image_url)
SELECT m ->> 'name', slug, m ->> 'address', m ->> 'description', m ->> 'website',
       m ->> 'email', m ->> 'phone', m ->> 'imageUrl'
FROM museum_src;

INSERT INTO floor_plans (museum_id, floor, label, image_url, north, south, east, west)
SELECT mu.id, (fp ->> 'floor')::int, fp ->> 'label', fp ->> 'imageUrl',
       (fp -> 'bounds' ->> 'north')::float8, (fp -> 'bounds' ->> 'south')::float8,
       (fp -> 'bounds' ->> 'east')::float8, (fp -> 'bounds' ->> 'west')::float8
FROM museum_src s
JOIN museums mu ON mu.slug = s.slug
CROSS JOIN jsonb_array_elements(s.m -> 'floorPlans') AS fp;

INSERT INTO points_of_interest (museum_id, type, label, lat, lng, floor)
SELECT mu.id, (p ->> 'type')::poi_type, p ->> 'label',
       (p -> 'coordinates' ->> 'lat')::float8, (p -> 'coordinates' ->> 'lng')::float8,
       coalesce((p ->> 'floor')::int, 0)
FROM museum_src s
JOIN museums mu ON mu.slug = s.slug
CROSS JOIN jsonb_array_elements(s.m -> 'pointsOfInterest') AS p;

INSERT INTO contents (universal_id, museum_id, type, name, author, year, image_url, lat, lng, floor)
SELECT c ->> 'universalId', mu.id, (c ->> 'type')::content_type, c ->> 'name', c ->> 'author',
       c ->> 'year', c ->> 'imageUrl',
       (c -> 'coordinates' ->> 'lat')::float8, (c -> 'coordinates' ->> 'lng')::float8,
       coalesce((c ->> 'floor')::int, 0)
FROM doc
CROSS JOIN museum_src s
JOIN museums mu ON mu.slug = s.slug
CROSS JOIN jsonb_array_elements(doc.j -> 'contents') AS c;

COMMIT;
