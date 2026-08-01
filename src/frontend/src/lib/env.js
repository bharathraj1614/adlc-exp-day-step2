export function getApiBaseUrl() {
  const w = window;
  const raw = w.__VITE_API_URL__;
  const base = typeof raw === 'string' ? raw : '';
  return base.replace(/\/+$/, '');
}
