import { readFileSync } from "node:fs";
import { join } from "node:path";

// The C# generator tests (tests/FFXIV.Tests/StaticData/StaticDataGoldenTests.cs) write these
// over the committed fixture; the ports replay them here.
const goldenDir = join(__dirname, "..", "..", "..", "..", "..", "tests", "FFXIV.Tests", "StaticData", "TestData", "goldens");

export function readGoldenLines<T>(fileName: string): T[] {
  return readFileSync(join(goldenDir, fileName), "utf8")
    .split("\n")
    .filter((line) => line.length > 0)
    .map((line) => JSON.parse(line) as T);
}
