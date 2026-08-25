import { NextResponse } from "next/server";
import { getSystemContext } from "@/lib/system-context";
import { ANTI_CHEATS } from "@/lib/system-context";

export async function GET() {
  try {
    const context = await getSystemContext();
    return NextResponse.json({
      success: true,
      data: {
        context,
        antiCheatCatalog: ANTI_CHEATS.map(({ id, name, requiresSecureBoot, noteEs, noteEn }) => ({
          id,
          name,
          expectsSecureBootOnWin11: requiresSecureBoot ?? false,
          noteEs,
          noteEn,
        })),
      },
      timestamp: new Date().toISOString(),
    });
  } catch (error) {
    console.error('Error collecting system context:', error);
    return NextResponse.json(
      { success: false, error: 'Could not collect the system context.' },
      { status: 500 }
    );
  }
}
