import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  getApiAvailability,
  outcomeForStatus,
  probeApi,
  reportApiOutcome,
  resetApiStatusForTests,
  subscribeApiAvailability,
} from "../api-status";

describe("api status", () => {
  beforeEach(() => {
    resetApiStatusForTests();
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.unstubAllGlobals();
  });

  it("starts unknown and follows what the API answered", () => {
    expect(getApiAvailability()).toBe("unknown");
    const seen: string[] = [];
    subscribeApiAvailability(() => seen.push(getApiAvailability()));

    reportApiOutcome("answered");
    reportApiOutcome("answered");
    reportApiOutcome("unreachable");

    expect(seen).toEqual(["available", "unavailable"]);
  });

  it("treats gateway errors as the API being down and everything else as an answer", () => {
    expect(outcomeForStatus(200)).toBe("answered");
    expect(outcomeForStatus(401)).toBe("answered");
    expect(outcomeForStatus(500)).toBe("answered");
    expect(outcomeForStatus(502)).toBe("unreachable");
    expect(outcomeForStatus(503)).toBe("unreachable");
    expect(outcomeForStatus(504)).toBe("unreachable");
  });

  it("probes the liveness endpoint and marks the API available again", async () => {
    const fetchMock = vi.fn().mockResolvedValue({ ok: true, status: 200 });
    vi.stubGlobal("fetch", fetchMock);

    reportApiOutcome("unreachable");
    expect(getApiAvailability()).toBe("unavailable");

    await expect(probeApi()).resolves.toBe(true);
    expect(getApiAvailability()).toBe("available");
    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(String(fetchMock.mock.calls[0][0])).toMatch(/\/health\/live$/);
  });

  it("a failed probe keeps the API unavailable", async () => {
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new TypeError("Failed to fetch")));

    reportApiOutcome("unreachable");
    await expect(probeApi()).resolves.toBe(false);
    expect(getApiAvailability()).toBe("unavailable");
  });
});
