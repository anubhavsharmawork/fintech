import React from 'react';
import ReactDOM from 'react-dom/client';
import './index.css';
import App from './App';
import reportWebVitals from './reportWebVitals';
import { ToastProvider } from './components/Toast';

const root = ReactDOM.createRoot(
  document.getElementById('root') as HTMLElement
);
root.render(
  <React.StrictMode>
    <ToastProvider>
      <App />
    </ToastProvider>
  </React.StrictMode>
);

reportWebVitals((metric) => {
  // send to analytics endpoint if desired
  // fetch('/analytics', { method: 'POST', body: JSON.stringify(metric) });
  // temporary: log to console
  // eslint-disable-next-line no-console
  console.debug('WebVital:', metric.name, Math.round(metric.value * 100) / 100);
});