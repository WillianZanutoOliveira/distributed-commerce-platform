CREATE ROLE products_runtime NOLOGIN;
CREATE ROLE products_migrator NOLOGIN;

GRANT CONNECT ON DATABASE products TO products_runtime;
GRANT CONNECT ON DATABASE products TO products_migrator;

GRANT USAGE ON SCHEMA public TO products_runtime;
GRANT USAGE, CREATE ON SCHEMA public TO products_migrator;

ALTER DEFAULT PRIVILEGES FOR ROLE products_migrator IN SCHEMA public
  GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO products_runtime;

ALTER DEFAULT PRIVILEGES FOR ROLE products_migrator IN SCHEMA public
  GRANT USAGE, SELECT, UPDATE ON SEQUENCES TO products_runtime;
