import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  output: "standalone",
  /* config options here */
  reactStrictMode: false,
  serverExternalPackages: ["systeminformation"],
};

export default nextConfig;
