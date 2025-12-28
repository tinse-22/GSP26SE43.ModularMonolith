CREATE TABLE IF NOT EXISTS "CacheEntries" (
	"Id" VARCHAR(449) NOT NULL PRIMARY KEY,
	"Value" BYTEA NOT NULL,
	"ExpiresAtTime" TIMESTAMPTZ NOT NULL,
	"SlidingExpirationInSeconds" BIGINT NULL,
	"AbsoluteExpiration" TIMESTAMPTZ NULL
);

CREATE INDEX IF NOT EXISTS "Index_ExpiresAtTime" ON "CacheEntries" ("ExpiresAtTime" ASC);
