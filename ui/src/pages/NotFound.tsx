import * as React from 'react';
import { Link } from 'react-router-dom';

const NotFound: React.FC = () => (
  <div style={{ textAlign: 'center', padding: '4rem 1rem' }}>
    <div style={{ fontSize: '4rem', marginBottom: '1rem' }}>404</div>
    <h2 style={{ marginBottom: '0.5rem' }}>Page Not Found</h2>
    <p style={{ color: 'var(--muted, #6b7280)', marginBottom: '2rem' }}>
      The page you are looking for does not exist or has been moved.
    </p>
    <Link to="/" className="btn btn-primary">Go to Dashboard</Link>
  </div>
);

export default NotFound;
