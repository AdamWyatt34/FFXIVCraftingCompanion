import { afterAll, beforeAll, describe, expect, it, vi } from "vitest";
import { ApiClientError } from "@/lib/api-errors";
import type { RecipeCraftingParametersDto, RecipeDto } from "@/types/api";
import type { CrafterStatsInput, HQIngredientInput } from "@/types/simulator";
import type { SolverWorkerRequest } from "@/types/solver-worker";

type RequiredStats = Pick<RecipeDto, "requiredCraftsmanship" | "requiredControl">;

const data = vi.hoisted(() => ({
  parameters: null as RecipeCraftingParametersDto | null,
  recipe: null as RequiredStats | null,
}));

vi.mock("@sentry/nextjs", () => ({ captureException: vi.fn() }));
vi.mock("@/lib/crafting-engine", () => ({ getRecipeParameters: async () => data.parameters }));
vi.mock("@/lib/game-data", () => ({ getRecipe: async () => data.recipe }));

const { checkRecipeMinimums, quickSolve, startSolve } = await import("@/lib/solver-client");

const STATS: CrafterStatsInput = { level: 100, craftsmanship: 4100, control: 3900, cp: 600 };

function rejection(run: () => unknown): ApiClientError | null {
  try {
    run();
    return null;
  } catch (err) {
    return err as ApiClientError;
  }
}

describe("checkRecipeMinimums", () => {
  it("never rejects a recipe without a requirement", () => {
    const none = { requiredCraftsmanship: 0, requiredControl: 0 };
    expect(rejection(() => checkRecipeMinimums(none, { ...STATS, craftsmanship: 1, control: 0 }))).toBeNull();
  });

  it("rejects with 422 below a required stat", () => {
    const craft = rejection(() => checkRecipeMinimums({ requiredCraftsmanship: 4200, requiredControl: 0 }, STATS));
    expect(craft).toBeInstanceOf(ApiClientError);
    expect(craft?.status).toBe(422);
    expect(craft?.message).toBe("Craftsmanship 4100 is below the recipe's required 4200.");

    const control = rejection(() => checkRecipeMinimums({ requiredCraftsmanship: 0, requiredControl: 4000 }, STATS));
    expect(control?.status).toBe(422);
    expect(control?.message).toBe("Control 3900 is below the recipe's required 4000.");
  });

  it("accepts stats at the requirement", () => {
    const met = { requiredCraftsmanship: 4100, requiredControl: 3900 };
    expect(rejection(() => checkRecipeMinimums(met, STATS))).toBeNull();
  });
});

const recommendedAbove: RecipeCraftingParametersDto = {
  recipeId: 1,
  recipeLevel: 690,
  difficulty: 6600,
  quality: 12000,
  durability: 70,
  progressDivider: 170,
  progressModifier: 90,
  qualityDivider: 150,
  qualityModifier: 75,
  classJobLevel: 100,
  stars: 0,
  isExpert: false,
  conditionsFlag: 0,
  suggestedCraftsmanship: 4207,
  suggestedControl: 3996,
  materialQualityFactor: 0,
  ingredients: [],
};

describe("startSolve", () => {
  it("rejects below a required stat before solving", async () => {
    data.parameters = { ...recommendedAbove, requiredCraftsmanship: 4300, requiredControl: 0 };
    data.recipe = { requiredCraftsmanship: 4300, requiredControl: 0 };
    await expect(startSolve({ recipeId: 1, stats: STATS })).rejects.toMatchObject({ status: 422 });
  });

  it("does not reject below the recommended stats", async () => {
    data.parameters = { ...recommendedAbove, requiredCraftsmanship: 0, requiredControl: 0 };
    data.recipe = { requiredCraftsmanship: 0, requiredControl: 0 };
    // Past the gate the solve needs a Worker, which node does not have.
    await expect(startSolve({ recipeId: 1, stats: STATS })).rejects.toThrow(/Worker/);
  });

  it("rejects a starting quality the engine's 32-bit input cannot hold before solving", async () => {
    data.parameters = { ...recommendedAbove, requiredCraftsmanship: 0, requiredControl: 0 };
    data.recipe = { requiredCraftsmanship: 0, requiredControl: 0 };
    for (const startingQuality of [2 ** 32, -1, 0.5, Number.NaN]) {
      await expect(startSolve({ recipeId: 1, stats: STATS, startingQuality })).rejects.toMatchObject({
        status: 400,
        message: "Starting quality must be a whole number from 0 to 2147483647.",
      });
    }
  });

  it("gates on the recipe's value when the crafting parameters carry no required stats", async () => {
    data.parameters = recommendedAbove;
    data.recipe = { requiredCraftsmanship: 4207, requiredControl: 0 };
    await expect(startSolve({ recipeId: 1, stats: STATS })).rejects.toMatchObject({
      status: 422,
      message: "Craftsmanship 4100 is below the recipe's required 4207.",
    });
  });
});

describe("quickSolve", () => {
  it("rejects below a required stat before solving, as startSolve does", async () => {
    data.parameters = recommendedAbove;
    data.recipe = { requiredCraftsmanship: 4207, requiredControl: 0 };
    await expect(quickSolve({ recipeId: 1, ...STATS })).rejects.toMatchObject({
      status: 422,
      message: "Craftsmanship 4100 is below the recipe's required 4207.",
    });
  });

  it("rejects a starting quality the engine's 32-bit input cannot hold before solving", async () => {
    data.parameters = recommendedAbove;
    for (const startingQuality of [2 ** 32, -1, 0.5, Number.POSITIVE_INFINITY]) {
      await expect(quickSolve({ recipeId: 1, ...STATS, startingQuality })).rejects.toMatchObject({
        status: 400,
        message: "Starting quality must be a whole number from 0 to 2147483647.",
      });
    }
  });
});

// Last in the file: solver-client keeps the first Worker it creates.
describe("the request the solver worker receives", () => {
  const posted: SolverWorkerRequest[] = [];
  const hqIngredients: HQIngredientInput[] = [{ itemId: 37830, hqQuantity: 2 }];

  beforeAll(() => {
    vi.stubGlobal(
      "Worker",
      class {
        onmessage = null;
        onerror = null;
        postMessage(message: SolverWorkerRequest) {
          posted.push(message);
        }
        terminate() {}
      },
    );
  });
  afterAll(() => vi.unstubAllGlobals());

  async function postedBy(solve: (signal: AbortSignal) => Promise<unknown>): Promise<SolverWorkerRequest> {
    const controller = new AbortController();
    const index = posted.length;
    const solving = solve(controller.signal);
    await vi.waitFor(() => expect(posted.length).toBeGreaterThan(index));
    controller.abort();
    await expect(solving).rejects.toThrow("Aborted");
    return posted[index];
  }

  it("carries Quick Solve's HQ materials and starting quality", async () => {
    data.parameters = recommendedAbove;
    data.recipe = { requiredCraftsmanship: 0, requiredControl: 0 };
    const request = await postedBy((signal) =>
      quickSolve({ recipeId: 1, ...STATS, hqIngredients, startingQuality: 350 }, signal),
    );
    expect(request).toMatchObject({ type: "solve", algorithm: "greedy", hqIngredients, startingQuality: 350 });
  });

  it("carries a full solve's HQ materials and starting quality", async () => {
    data.parameters = recommendedAbove;
    data.recipe = { requiredCraftsmanship: 0, requiredControl: 0 };
    const request = await postedBy((signal) =>
      startSolve({ recipeId: 1, stats: STATS, hqIngredients, startingQuality: 350 }, { signal }),
    );
    expect(request).toMatchObject({ type: "solve", hqIngredients, startingQuality: 350 });
  });
});
