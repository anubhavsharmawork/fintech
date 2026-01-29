import * as React from 'react';

interface Props {
  enabled: boolean;
  onDismiss?: () => void;
}

const CryptoModeBanner: React.FC<Props> = ({ enabled, onDismiss }) => {
  if (!enabled) return null;
  const handleDismiss = (e: React.KeyboardEvent | React.MouseEvent) => {
    if (e instanceof KeyboardEvent && e.key !== 'Enter' && e.key !== ' ') {
      return;
    }
    onDismiss?.();
  };

  return (
    <div 
      role="region" 
      aria-live="polite" 
      aria-label="F-Mode notification"
      style={{ 
        background: '#14b8a6', 
        color: '#fff', 
        padding: '10px 16px', 
        borderRadius: 8, 
        marginBottom: 12, 
        display: 'flex', 
        alignItems: 'center', 
        gap: 12 
      }}
    >
      <div>
        <strong>F-Mode enabled:</strong> showing crypto accounts and features
      </div>
      <button 
        className="btn btn-secondary" 
        type="button" 
        onClick={onDismiss}
        onKeyDown={handleDismiss}
        aria-label="Dismiss F-Mode notification"
      >
        Dismiss
      </button>
    </div>
  );
};

export default CryptoModeBanner;
