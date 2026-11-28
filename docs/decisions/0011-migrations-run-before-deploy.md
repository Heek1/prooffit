# 0011. Migrations run in CI before the API is deployed

## Context

The API applied migrations to its database when it started (`Database:MigrateOnStartup`). That ties a schema change to whichever instance boots first, gives the running service the right to alter its own tables, and turns a failed migration into a service that will not start. Render's free plan has no pre-deploy command to run them separately.

## Decision

On every commit to main, a CI job builds an EF Core migrations bundle and applies it to the production database once the tests have passed. Render deploys a commit only after its checks pass, and that job is one of them. The schema is therefore up to date before the new code starts, and a migration that fails stops the deploy instead of the service.

The bundle reads its connection string the way the API does, from `ConnectionStrings:Default`, which is held as a GitHub secret. EF Core takes a lock on the database while it migrates, and the job runs one at a time.

Docker Compose and local development keep `MigrateOnStartup`, where a single developer's database has nothing to protect. Production turns it off.

No backups are taken. Comparisons and their documents are deleted after a day (record 0007), so a copy would mostly keep people's resumes for longer than promised. The schema itself is rebuilt from the migrations.

## Consequences

For a few minutes the previous version of the API runs against the new schema. A migration must keep working for it: add a column or table in one release, and drop the old one in a later release once nothing reads it. CI needs the database's credentials, so the secret lives in GitHub as well as in Render. A commit whose migration has not run, because the job failed or the secret is missing, is not deployed.
