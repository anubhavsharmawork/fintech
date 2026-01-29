import * as React from 'react';

const Privacy: React.FC = () => {
  return (
    <div style={{ maxWidth: 800, margin: '0 auto', padding: 20 }}>
      <h1 style={{ fontSize: 28, marginBottom: 20 }}>Privacy Policy</h1>
      
      <h2 style={{ fontSize: 18, marginTop: 24, marginBottom: 12 }}>Data Collection</h2>
      <p style={{ marginBottom: 12 }}>
        This application collects no personal data. No contact form, no analytics, no tracking.
      </p>
      
      <h2 style={{ fontSize: 18, marginTop: 24, marginBottom: 12 }}>Server Logs</h2>
      <p style={{ marginBottom: 12 }}>
        IP addresses and request metadata are logged by the CDN for security purposes only and auto-deleted.
      </p>
      
      <h2 style={{ fontSize: 18, marginTop: 24, marginBottom: 12 }}>Your Rights (NZ Privacy Act 2020)</h2>
      <p style={{ marginBottom: 12 }}>
        If you have concerns about privacy, contact the address below.
      </p>
      
      <h2 style={{ fontSize: 18, marginTop: 24, marginBottom: 12 }}>Security</h2>
      <ul style={{ marginBottom: 12, paddingLeft: 20 }}>
        <li>HTTPS enforced (TLS 1.2+)</li>
        <li>Dependencies scanned weekly</li>
      </ul>
      
      <h2 style={{ fontSize: 18, marginTop: 24, marginBottom: 12 }}>Third-Party Services</h2>
      <ul style={{ marginBottom: 12, paddingLeft: 20 }}>
        <li><strong>Hosting:</strong> Salesforce Heroku</li>
      </ul>
      
      <div style={{ 
        background: 'var(--card-bg)', 
        padding: 16, 
        borderRadius: 8, 
        marginTop: 20,
        border: '1px solid var(--border)'
      }}>
        <h2 style={{ fontSize: 18, marginTop: 0, marginBottom: 12 }}>Contact</h2>
        <p style={{ margin: 0 }}>
          <strong>Name:</strong> Anubhav Sharma<br />
          <strong>Email:</strong>{' '}
          <a href="mailto:anubhav.sharma.work@outlook.com" style={{ color: 'var(--primary)' }}>
            anubhav.sharma.work@outlook.com
          </a>
        </p>
      </div>
      
      <div style={{ 
        fontSize: 12, 
        color: 'var(--text-secondary)', 
        marginTop: 40, 
        paddingTop: 20, 
        borderTop: '1px solid var(--border)' 
      }}>
        <p><strong>Last Updated:</strong> 29 Jan 2026</p>
      </div>
    </div>
  );
};

export default Privacy;
