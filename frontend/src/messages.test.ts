import { describe, it, expect } from "vitest";
import { ApiError } from "./api";
import { errorMessage } from "./messages";

describe("errorMessage", () => {
  it("maps a known API error code to a Bangla message", () => {
    expect(errorMessage(new ApiError("NO_SEATS", "x", 409))).toContain("seat");
  });

  it("falls back to a generic message for unknown errors", () => {
    expect(errorMessage(new Error("boom"))).toBe(
      "কিছু একটা ভুল হয়েছে। আবার চেষ্টা করুন।",
    );
  });
});
