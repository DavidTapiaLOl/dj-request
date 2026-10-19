import * as React from "react";
import { Slot } from "expo-router";
import { AuthProvider } from "../src/auth/provider";

export default function RootLayout() {
  return (
    <AuthProvider>
      <Slot />
    </AuthProvider>
  );
}