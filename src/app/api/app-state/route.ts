import { NextRequest, NextResponse } from "next/server";
import { readAppState, updateAppState, AppStateFlags } from "@/lib/app-state";

interface ApiResponse {
  success: boolean;
  data?: unknown;
  error?: string;
}

/** GET /api/app-state → persistent UI flags (onboarding, splash). */
export async function GET() {
  return NextResponse.json<ApiResponse>({ success: true, data: readAppState() });
}

/** POST /api/app-state → merge and persist UI flags. */
export async function POST(request: NextRequest) {
  try {
    const body: unknown = await request.json();
    if (!body || typeof body !== 'object' || Array.isArray(body)) {
      return NextResponse.json<ApiResponse>(
        { success: false, error: "Request body must be an object" },
        { status: 400 }
      );
    }

    const patch = body as Record<string, unknown>;
    const allowedKeys: (keyof AppStateFlags)[] = ['onboardingCompleted', 'splashSeen'];
    for (const [key, value] of Object.entries(patch)) {
      if (!allowedKeys.includes(key as keyof AppStateFlags) || typeof value !== 'boolean') {
        return NextResponse.json<ApiResponse>(
          { success: false, error: `Invalid field: ${key}. Only boolean onboardingCompleted/splashSeen are accepted.` },
          { status: 400 }
        );
      }
    }

    return NextResponse.json<ApiResponse>({ success: true, data: updateAppState(patch as AppStateFlags) });
  } catch (error) {
    console.error("Error updating app state:", error);
    return NextResponse.json<ApiResponse>(
      { success: false, error: "Failed to update app state" },
      { status: 500 }
    );
  }
}
