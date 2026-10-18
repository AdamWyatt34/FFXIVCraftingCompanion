import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  ApiClientError,
  authGetMe,
  clearInflightRequests,
  configureAuth,
  getNotifications,
  getRotationById,
} from "../api-client";
import { getApiAvailability, resetApiStatusForTests } from "../api-status";

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "content-type": "application/json" },
  });
}

describe("api client", () => {
  beforeEach(() => {
    resetApiStatusForTests();
    clearInflightRequests();
    configureAuth(() => null, () => {}, async () => false);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.useRealTimers();
  });

  it("surfaces a timeout as a 408 the UI can show, not as an AbortError", async () => {
    vi.useFakeTimers();
    vi.stubGlobal(
      "fetch",
      vi.fn((_url: string, init: RequestInit) =>
        new Promise<Response>((_resolve, reject) => {
          init.signal?.addEventListener("abort", () =>
            reject(new DOMException("The operation was aborted.", "AbortError")),
          );
        }),
      ),
    );

    const pending = getRotationById(1);
    const outcome = pending.then(
      () => "resolved",
      (err: unknown) => err,
    );
    await vi.advanceTimersByTimeAsync(10_001);

    const err = await outcome;
    expect(err).toBeInstanceOf(ApiClientError);
    expect((err as ApiClientError).status).toBe(408);
    expect(getApiAvailability()).toBe("unavailable");
  });

  it("passes the caller's own cancellation through untouched", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn((_url: string, init: RequestInit) =>
        new Promise<Response>((_resolve, reject) => {
          init.signal?.addEventListener("abort", () =>
            reject(new DOMException("The operation was aborted.", "AbortError")),
          );
        }),
      ),
    );

    const controller = new AbortController();
    const pending = getRotationById(1, controller.signal);
    controller.abort();

    await expect(pending).rejects.toMatchObject({ name: "AbortError" });
    expect(getApiAvailability()).toBe("unknown");
  });

  it("turns a network failure into a readable error and marks the API unreachable", async () => {
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new TypeError("Failed to fetch")));

    await expect(getRotationById(1)).rejects.toMatchObject({
      status: 0,
      message: expect.stringContaining("Could not reach the server"),
    });
    expect(getApiAvailability()).toBe("unavailable");
  });

  it("marks the API available on any answer, including a 404", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(jsonResponse({ error: "Rotation not found." }, 404)));

    await expect(getRotationById(1)).rejects.toMatchObject({ status: 404 });
    expect(getApiAvailability()).toBe("available");
  });

  it("deduplicates concurrent GETs per bearer token and forgets them on demand", async () => {
    const fetchMock = vi.fn().mockImplementation(() => Promise.resolve(jsonResponse([])));
    vi.stubGlobal("fetch", fetchMock);

    let token: string | null = "token-one";
    configureAuth(() => token, () => {}, async () => false);

    await Promise.all([getNotifications(), getNotifications()]);
    expect(fetchMock).toHaveBeenCalledTimes(1);

    // A different identity must not receive the first user's in-flight response.
    const slow = vi.fn().mockImplementation(() => new Promise<Response>(() => {}));
    vi.stubGlobal("fetch", slow);
    void getNotifications();
    token = "token-two";
    void getNotifications();
    expect(slow).toHaveBeenCalledTimes(2);

    clearInflightRequests();
    void getNotifications();
    expect(slow).toHaveBeenCalledTimes(3);
  });

  it("refreshes once on a 401 and retries with the new token", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(jsonResponse({ error: "expired" }, 401))
      .mockResolvedValueOnce(jsonResponse({ userId: "u1" }));
    vi.stubGlobal("fetch", fetchMock);

    let token = "stale";
    const refresh = vi.fn(async () => {
      token = "fresh";
      return true;
    });
    configureAuth(() => token, () => {}, refresh);

    await expect(authGetMe()).resolves.toEqual({ userId: "u1" });
    expect(refresh).toHaveBeenCalledTimes(1);
    const secondHeaders = fetchMock.mock.calls[1][1].headers as Record<string, string>;
    expect(secondHeaders.Authorization).toBe("Bearer fresh");
  });

  it("signs the user out when the refresh fails after a 401", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(jsonResponse({ error: "expired" }, 401)));
    const onUnauthorized = vi.fn();
    configureAuth(() => "stale", onUnauthorized, async () => false);

    await expect(authGetMe()).rejects.toMatchObject({ status: 401 });
    expect(onUnauthorized).toHaveBeenCalledTimes(1);
  });
});
