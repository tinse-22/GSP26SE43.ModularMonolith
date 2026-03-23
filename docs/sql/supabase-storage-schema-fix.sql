-- Run this script in Supabase SQL Editor as role: supabase_admin
-- Goal: allow app role `postgres` to run EF Core migrations in schema `storage`.

BEGIN;

-- 1) Preflight
SELECT current_database() AS db_name, current_schema() AS schema_name, current_user AS current_role, session_user AS session_role;

SELECT n.nspname AS schema_name,
       pg_get_userbyid(n.nspowner) AS schema_owner
FROM pg_namespace n
WHERE n.nspname = 'storage';

-- 2) Ensure schema exists
CREATE SCHEMA IF NOT EXISTS storage;

-- 3) Grant schema-level privileges
GRANT USAGE, CREATE ON SCHEMA storage TO postgres;

-- 4) Grant privileges on existing objects
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA storage TO postgres;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA storage TO postgres;
GRANT ALL PRIVILEGES ON ALL FUNCTIONS IN SCHEMA storage TO postgres;

-- 5) Ensure future objects created by owner also grant to postgres
ALTER DEFAULT PRIVILEGES FOR ROLE supabase_admin IN SCHEMA storage
GRANT ALL PRIVILEGES ON TABLES TO postgres;

ALTER DEFAULT PRIVILEGES FOR ROLE supabase_admin IN SCHEMA storage
GRANT ALL PRIVILEGES ON SEQUENCES TO postgres;

ALTER DEFAULT PRIVILEGES FOR ROLE supabase_admin IN SCHEMA storage
GRANT ALL PRIVILEGES ON FUNCTIONS TO postgres;

-- 6) Verification
SELECT 'storage' AS schema_name,
       has_schema_privilege('postgres', 'storage', 'USAGE') AS postgres_has_usage,
       has_schema_privilege('postgres', 'storage', 'CREATE') AS postgres_has_create;

COMMIT;
