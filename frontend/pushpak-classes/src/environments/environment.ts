declare global {
  interface Window {
    __env?: {
      apiUrl?: string;
      appName?: string;
    };
  }
}

export const environment = {
  production: true,
  apiUrl: (typeof window !== 'undefined' && window.__env?.apiUrl) || 'http://localhost:5139/api',
  appName: (typeof window !== 'undefined' && window.__env?.appName) || 'PushpakClasses'
};
