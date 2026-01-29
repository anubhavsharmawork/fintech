/**
 * Accessibility Utilities for WCAG 2.1 AA Compliance
 * Provides helpers for keyboard navigation, focus management, ARIA attributes,
 * and screen reader support
 */

/**
 * Announces a message to screen readers using ARIA live regions
 */
export const announceToScreenReader = (message: string, priority: 'polite' | 'assertive' = 'polite') => {
  const announcement = document.createElement('div');
  announcement.setAttribute('role', 'status');
  announcement.setAttribute('aria-live', priority);
  announcement.setAttribute('aria-atomic', 'true');
  announcement.style.position = 'absolute';
  announcement.style.left = '-10000px';
  announcement.style.width = '1px';
  announcement.style.height = '1px';
  announcement.style.overflow = 'hidden';
  announcement.textContent = message;
  
  document.body.appendChild(announcement);
  
  setTimeout(() => {
    document.body.removeChild(announcement);
  }, 1000);
};

/**
 * Manages focus for modal dialogs - traps focus within modal
 */
export const manageFocusTrap = (element: HTMLElement | null) => {
  if (!element) return;

  const focusableElements = element.querySelectorAll(
    'button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])'
  );
  
  const firstElement = focusableElements[0] as HTMLElement;
  const lastElement = focusableElements[focusableElements.length - 1] as HTMLElement;

  element.addEventListener('keydown', (e) => {
    if (e.key !== 'Tab') return;

    if (e.shiftKey) {
      if (document.activeElement === firstElement) {
        lastElement?.focus();
        e.preventDefault();
      }
    } else {
      if (document.activeElement === lastElement) {
        firstElement?.focus();
        e.preventDefault();
      }
    }
  });
};

/**
 * Restores focus to a previously focused element
 */
export const restoreFocus = () => {
  const savedElement = document.activeElement as HTMLElement | null;
  return () => savedElement?.focus();
};

/**
 * Generates ARIA label for screen readers
 */
export const generateAriaLabel = (text: string, context?: string): string => {
  return context ? `${text}, ${context}` : text;
};

/**
 * Checks if user prefers reduced motion
 */
export const prefersReducedMotion = (): boolean => {
  return window.matchMedia('(prefers-reduced-motion: reduce)').matches;
};

/**
 * Ensures proper heading hierarchy
 */
export const validateHeadingHierarchy = (element: HTMLElement): string[] => {
  const headings = Array.from(element.querySelectorAll('h1, h2, h3, h4, h5, h6'));
  const hierarchy: string[] = [];
  let lastLevel = 0;

  headings.forEach((heading) => {
    const level = parseInt(heading.tagName[1]);
    if (level > lastLevel + 1) {
      hierarchy.push(`Warning: Skipped heading level from H${lastLevel} to H${level}`);
    }
    lastLevel = level;
  });

  return hierarchy;
};

/**
 * Makes skip link functional
 */
export const setupSkipLink = () => {
  const skipLink = document.querySelector('a.skip-link') as HTMLAnchorElement;
  if (!skipLink) return;

  skipLink.addEventListener('click', (e) => {
    e.preventDefault();
    const mainContent = document.getElementById('main-content');
    if (mainContent) {
      mainContent.focus();
      mainContent.scrollIntoView();
    }
  });
};

/**
 * Adds keyboard navigation to custom buttons
 */
export const enableKeyboardNavigation = (element: HTMLElement) => {
  if (element.getAttribute('role') !== 'button') return;

  element.addEventListener('keydown', (e) => {
    if (e.key === 'Enter' || e.key === ' ') {
      e.preventDefault();
      (element as HTMLElement).click();
    }
  });
};

/**
 * Tests color contrast ratio (WCAG 2.1 AA requires 4.5:1 for normal text, 3:1 for large)
 */
export const getContrastRatio = (color1: string, color2: string): number => {
  const getLuminance = (color: string): number => {
    const rgb = color.match(/\d+/g);
    if (!rgb || rgb.length < 3) return 0;

    const [r, g, b] = rgb.map((val) => {
      const v = parseInt(val) / 255;
      return v <= 0.03928 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4);
    });

    return 0.2126 * r + 0.7152 * g + 0.0722 * b;
  };

  const l1 = getLuminance(color1);
  const l2 = getLuminance(color2);
  const lighter = Math.max(l1, l2);
  const darker = Math.min(l1, l2);

  return (lighter + 0.05) / (darker + 0.05);
};

/**
 * Validates ARIA attributes on element
 */
export const validateAriaAttributes = (element: HTMLElement): string[] => {
  const warnings: string[] = [];
  const role = element.getAttribute('role');

  if (element.tagName === 'BUTTON' && !element.hasAttribute('type')) {
    warnings.push('Button missing type attribute');
  }

  if (element.tagName === 'IMG' && !element.hasAttribute('alt')) {
    warnings.push('Image missing alt text');
  }

  if (role === 'button' && !element.hasAttribute('tabindex')) {
    warnings.push('Custom button should have tabindex="0"');
  }

  if ((element.tagName === 'INPUT' || element.tagName === 'SELECT' || element.tagName === 'TEXTAREA') && !element.closest('label')) {
    warnings.push('Form field should be associated with a label');
  }

  return warnings;
};

/**
 * Initializes all accessibility features
 */
export const initializeAccessibility = () => {
  setupSkipLink();
  
  // Add keyboard navigation to custom buttons
  document.querySelectorAll('[role="button"]').forEach((btn) => {
    enableKeyboardNavigation(btn as HTMLElement);
  });

  // Validate heading hierarchy
  const warnings = validateHeadingHierarchy(document.body);
  if (warnings.length > 0 && process.env.NODE_ENV === 'development') {
    console.warn('Accessibility warnings:', warnings);
  }
};

export default {
  announceToScreenReader,
  manageFocusTrap,
  restoreFocus,
  generateAriaLabel,
  prefersReducedMotion,
  validateHeadingHierarchy,
  setupSkipLink,
  enableKeyboardNavigation,
  getContrastRatio,
  validateAriaAttributes,
  initializeAccessibility,
};
