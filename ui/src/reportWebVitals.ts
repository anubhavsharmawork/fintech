import { onCLS, onFID, onLCP, onINP, onTTFB, type Metric } from 'web-vitals';

export default function reportWebVitals(onReport: (metric: Metric) => void = console.log): void {
  try {
    onCLS(onReport);
    onFID(onReport);
    onLCP(onReport);
    onINP(onReport);
    onTTFB(onReport);
  } catch {
    // ignore if web-vitals unavailable
  }
}
