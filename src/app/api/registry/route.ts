import { NextRequest, NextResponse } from "next/server";

interface RegistryApiResponse {
  success: false;
  error: string;
  message: string;
  timestamp: string;
}

function unavailableResponse(): NextResponse<RegistryApiResponse> {
  return NextResponse.json(
    {
      success: false,
      error: "Registry access is unavailable until a real Windows Registry adapter is implemented.",
      message: "The previous implementation was simulated and has been disabled to prevent false system changes.",
      timestamp: new Date().toISOString(),
    },
    { status: 501 },
  );
}

export async function GET(_request: NextRequest): Promise<NextResponse<RegistryApiResponse>> {
  return unavailableResponse();
}

export async function POST(_request: NextRequest): Promise<NextResponse<RegistryApiResponse>> {
  return unavailableResponse();
}
