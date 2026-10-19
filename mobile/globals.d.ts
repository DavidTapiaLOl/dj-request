declare module "*.module.css" {
  const classes: Readonly<Record<string, string>>;
  export default classes;
}

declare global {
  namespace NodeJS {
    interface ProcessEnv {
      EXPO_PUBLIC_API_URL?: string;
    }
  }
}

export {};