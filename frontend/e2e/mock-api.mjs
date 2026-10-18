// A stand-in for the .NET API during the e2e run. Serves the recipe fixtures with CORS, answers
// the solver endpoints with a marker error (so an API-mode solve is recognisable), records every
// request, and can be switched "offline", after which it drops API connections the way a lost
// network does. The e2e build points NEXT_PUBLIC_API_URL here; requests made by the service
// worker cannot be intercepted by Playwright, so they must reach a server the test controls.

import { createServer } from "node:http";
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const port = Number(process.env.MOCK_API_PORT ?? 3101);
const fixtures = join(dirname(fileURLToPath(import.meta.url)), "fixtures");
const RECIPE_ID = 37783;

const recipe = readFileSync(join(fixtures, `recipe-${RECIPE_ID}.json`), "utf8");
const parameters = readFileSync(join(fixtures, `recipe-${RECIPE_ID}-crafting-parameters.json`), "utf8");

let offline = false;
const requests = [];

const CORS = {
  "access-control-allow-origin": "*",
  "access-control-allow-headers": "*",
  "access-control-allow-methods": "GET,POST,PUT,DELETE,OPTIONS",
};

function json(res, status, body) {
  res.writeHead(status, { ...CORS, "content-type": "application/json" });
  res.end(body);
}

createServer((req, res) => {
  const url = new URL(req.url, `http://localhost:${port}`);
  const path = url.pathname;

  if (path === "/__offline") {
    offline = url.searchParams.get("on") !== "0";
    return json(res, 200, JSON.stringify({ offline }));
  }
  if (path === "/__requests") {
    return json(res, 200, JSON.stringify(requests));
  }
  if (path === "/__reset") {
    requests.length = 0;
    offline = false;
    return json(res, 200, "{}");
  }

  // The availability probe lives outside /api; it counts as a request to the API all the same.
  if (path.startsWith("/api/") || path.startsWith("/health")) requests.push(`${req.method} ${path}`);
  if (offline) {
    req.socket.destroy();
    return;
  }
  if (req.method === "OPTIONS") {
    res.writeHead(204, CORS);
    return res.end();
  }
  if (path === "/health/live") return json(res, 200, "{}");
  if (path === `/api/recipes/${RECIPE_ID}`) return json(res, 200, recipe);
  if (path === `/api/recipes/${RECIPE_ID}/crafting-parameters`) return json(res, 200, parameters);
  if (path.startsWith("/api/crafting/solve") || path.startsWith("/api/crafting/simulate")) {
    return json(res, 503, '{"error":"api path exercised"}');
  }
  return json(res, 404, '{"error":"not found"}');
}).listen(port, () => {
  console.log(`mock api listening on http://localhost:${port}`);
});
