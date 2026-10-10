# JobHunt frontend

Open this folder in VS Code. Run `npm install` on a fresh checkout, then `npm start`.
Visit http://localhost:4200. Run the backend with its `http` profile on port 5077.

See [the project guide](../../README.md) for the database setup, architecture,
request flow, verification commands, and next learning checkpoint.

- `npm start`: local development with API proxy.
- `npm run build`: production compilation.
- `npm run watch`: rebuild on source changes.

Feature code is under `src/app/features/job-applications`. Shared page framing is
under `src/app/layout`. UI validation uses reactive forms; backend validation is
also enforced. No applications or statistics are mocked.
