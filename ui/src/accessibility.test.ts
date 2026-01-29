import {
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
} from './accessibility';

describe('accessibility.ts', () => {
  let mockElement: HTMLElement;

  beforeEach(() => {
    jest.clearAllMocks();
    mockElement = document.createElement('div');
    document.body.appendChild(mockElement);
  });

  afterEach(() => {
    if (mockElement.parentNode) {
      mockElement.parentNode.removeChild(mockElement);
    }
  });

  describe('announceToScreenReader', () => {
    it('should create and append announcement element', () => {
      const removeSpy = jest.spyOn(document.body, 'removeChild');
      
      announceToScreenReader('Test announcement');

      const announcements = document.querySelectorAll('[role="status"]');
      expect(announcements.length).toBeGreaterThan(0);
    });

    it('should set correct ARIA attributes', () => {
      announceToScreenReader('Test message', 'assertive');

      const announcement = document.querySelector('[role="status"]') as HTMLElement;
      expect(announcement).not.toBeNull();
      expect(announcement.getAttribute('aria-live')).toBe('assertive');
      expect(announcement.getAttribute('aria-atomic')).toBe('true');
    });

    it('should use polite priority by default', () => {
      announceToScreenReader('Test message');

      const announcement = document.querySelector('[role="status"]') as HTMLElement;
      expect(announcement.getAttribute('aria-live')).toBe('polite');
    });

    it('should hide announcement visually', () => {
      announceToScreenReader('Test message');

      const announcement = document.querySelector('[role="status"]') as HTMLElement;
      expect(announcement.style.position).toBe('absolute');
      expect(announcement.style.left).toBe('-10000px');
      expect(announcement.style.overflow).toBe('hidden');
    });

    it('should set announcement text content', () => {
      announceToScreenReader('Important message');

      const announcement = document.querySelector('[role="status"]') as HTMLElement;
      expect(announcement.textContent).toBe('Important message');
    });

    it('should remove announcement after timeout', () => {
      jest.useFakeTimers();
      const removeSpy = jest.spyOn(document.body, 'removeChild');

      announceToScreenReader('Test message');

      jest.runAllTimers();

      expect(removeSpy).toHaveBeenCalled();
      jest.useRealTimers();
    });
  });

  describe('manageFocusTrap', () => {
    it('should not throw on null element', () => {
      expect(() => manageFocusTrap(null)).not.toThrow();
    });

    it('should trap focus when Tab is pressed on last element', () => {
      const button1 = document.createElement('button');
      const button2 = document.createElement('button');
      mockElement.appendChild(button1);
      mockElement.appendChild(button2);

      manageFocusTrap(mockElement);

      button2.focus();
      expect(document.activeElement).toBe(button2);

      const tabEvent = new KeyboardEvent('keydown', { key: 'Tab' });
      const preventDefaultSpy = jest.spyOn(tabEvent, 'preventDefault');
      button2.dispatchEvent(tabEvent);

      expect(preventDefaultSpy).toHaveBeenCalled();
    });

    it('should trap focus backwards on Shift+Tab from first element', () => {
      const button1 = document.createElement('button');
      const button2 = document.createElement('button');
      mockElement.appendChild(button1);
      mockElement.appendChild(button2);

      manageFocusTrap(mockElement);

      button1.focus();

      const tabEvent = new KeyboardEvent('keydown', {
        key: 'Tab',
        shiftKey: true,
      });
      const preventDefaultSpy = jest.spyOn(tabEvent, 'preventDefault');
      button1.dispatchEvent(tabEvent);

      expect(preventDefaultSpy).toHaveBeenCalled();
    });

    it('should find focusable elements within element', () => {
      const button = document.createElement('button');
      const input = document.createElement('input');
      const link = document.createElement('a');
      link.href = '#test';

      mockElement.appendChild(button);
      mockElement.appendChild(input);
      mockElement.appendChild(link);

      expect(() => manageFocusTrap(mockElement)).not.toThrow();
    });
  });

  describe('restoreFocus', () => {
    it('should return function that restores focus', () => {
      const button = document.createElement('button');
      mockElement.appendChild(button);
      button.focus();

      const restorer = restoreFocus();
      const otherButton = document.createElement('button');
      mockElement.appendChild(otherButton);
      otherButton.focus();

      expect(document.activeElement).not.toBe(button);

      restorer();

      expect(document.activeElement).toBe(button);
    });

    it('should handle null activeElement', () => {
      const restorer = restoreFocus();
      expect(() => restorer()).not.toThrow();
    });
  });

  describe('generateAriaLabel', () => {
    it('should return text without context', () => {
      const label = generateAriaLabel('Click me');
      expect(label).toBe('Click me');
    });

    it('should include context when provided', () => {
      const label = generateAriaLabel('Save', 'document');
      expect(label).toBe('Save, document');
    });

    it('should format multi-word text correctly', () => {
      const label = generateAriaLabel('Submit Form', 'sign up process');
      expect(label).toBe('Submit Form, sign up process');
    });
  });

  describe('prefersReducedMotion', () => {
    it('should return boolean', () => {
      const result = prefersReducedMotion();
      expect(typeof result).toBe('boolean');
    });

    it('should check media query', () => {
      const mediaQuerySpy = jest.spyOn(window, 'matchMedia');
      prefersReducedMotion();

      expect(mediaQuerySpy).toHaveBeenCalledWith('(prefers-reduced-motion: reduce)');
    });
  });

  describe('validateHeadingHierarchy', () => {
    it('should return empty array for correct hierarchy', () => {
      const h1 = document.createElement('h1');
      const h2 = document.createElement('h2');
      mockElement.appendChild(h1);
      mockElement.appendChild(h2);

      const warnings = validateHeadingHierarchy(mockElement);
      expect(warnings).toEqual([]);
    });

    it('should detect skipped heading levels', () => {
      const h1 = document.createElement('h1');
      const h3 = document.createElement('h3');
      mockElement.appendChild(h1);
      mockElement.appendChild(h3);

      const warnings = validateHeadingHierarchy(mockElement);
      expect(warnings.length).toBeGreaterThan(0);
      expect(warnings[0]).toContain('Skipped heading level');
    });

    it('should handle h4, h5, h6 tags', () => {
      const h1 = document.createElement('h1');
      const h2 = document.createElement('h2');
      const h3 = document.createElement('h3');
      const h4 = document.createElement('h4');
      const h5 = document.createElement('h5');
      const h6 = document.createElement('h6');

      mockElement.appendChild(h1);
      mockElement.appendChild(h2);
      mockElement.appendChild(h3);
      mockElement.appendChild(h4);
      mockElement.appendChild(h5);
      mockElement.appendChild(h6);

      const warnings = validateHeadingHierarchy(mockElement);
      expect(warnings).toEqual([]);
    });

    it('should return empty array when no headings', () => {
      const warnings = validateHeadingHierarchy(mockElement);
      expect(warnings).toEqual([]);
    });
  });

  describe('setupSkipLink', () => {
    it('should handle missing skip link', () => {
      expect(() => setupSkipLink()).not.toThrow();
    });

    it('should focus main content on skip link click', () => {
      const skipLink = document.createElement('a');
      skipLink.className = 'skip-link';
      skipLink.href = '#main-content';
      document.body.appendChild(skipLink);

      const mainContent = document.createElement('div');
      mainContent.id = 'main-content';
      mainContent.tabIndex = -1;
      document.body.appendChild(mainContent);

      const focusSpy = jest.spyOn(mainContent, 'focus');
      const scrollSpy = jest.spyOn(mainContent, 'scrollIntoView');

      setupSkipLink();
      skipLink.click();

      expect(focusSpy).toHaveBeenCalled();
      expect(scrollSpy).toHaveBeenCalled();

      skipLink.remove();
      mainContent.remove();
    });

    it('should prevent default on skip link click', () => {
      const skipLink = document.createElement('a');
      skipLink.className = 'skip-link';
      skipLink.href = '#main-content';
      document.body.appendChild(skipLink);

      const mainContent = document.createElement('div');
      mainContent.id = 'main-content';
      document.body.appendChild(mainContent);

      setupSkipLink();

      const event = new MouseEvent('click');
      const preventDefaultSpy = jest.spyOn(event, 'preventDefault');
      skipLink.dispatchEvent(event);

      expect(preventDefaultSpy).toHaveBeenCalled();

      skipLink.remove();
      mainContent.remove();
    });
  });

  describe('enableKeyboardNavigation', () => {
    it('should return early if not a button role', () => {
      const div = document.createElement('div');
      div.setAttribute('role', 'region');

      expect(() => enableKeyboardNavigation(div)).not.toThrow();
    });

    it('should trigger click on Enter key', () => {
      const button = document.createElement('div');
      button.setAttribute('role', 'button');
      const clickSpy = jest.spyOn(button, 'click');

      enableKeyboardNavigation(button);

      const event = new KeyboardEvent('keydown', { key: 'Enter' });
      const preventDefaultSpy = jest.spyOn(event, 'preventDefault');
      button.dispatchEvent(event);

      expect(preventDefaultSpy).toHaveBeenCalled();
      expect(clickSpy).toHaveBeenCalled();
    });

    it('should trigger click on Space key', () => {
      const button = document.createElement('div');
      button.setAttribute('role', 'button');
      const clickSpy = jest.spyOn(button, 'click');

      enableKeyboardNavigation(button);

      const event = new KeyboardEvent('keydown', { key: ' ' });
      const preventDefaultSpy = jest.spyOn(event, 'preventDefault');
      button.dispatchEvent(event);

      expect(preventDefaultSpy).toHaveBeenCalled();
      expect(clickSpy).toHaveBeenCalled();
    });

    it('should not prevent default for other keys', () => {
      const button = document.createElement('div');
      button.setAttribute('role', 'button');

      enableKeyboardNavigation(button);

      const event = new KeyboardEvent('keydown', { key: 'a' });
      const preventDefaultSpy = jest.spyOn(event, 'preventDefault');
      button.dispatchEvent(event);

      expect(preventDefaultSpy).not.toHaveBeenCalled();
    });
  });

  describe('getContrastRatio', () => {
    it('should return contrast ratio for valid colors', () => {
      const ratio = getContrastRatio('rgb(0, 0, 0)', 'rgb(255, 255, 255)');
      expect(ratio).toBeGreaterThan(0);
      expect(ratio).toBeCloseTo(21, 0); // Black and white should have very high contrast
    });

    it('should handle identical colors', () => {
      const ratio = getContrastRatio('rgb(128, 128, 128)', 'rgb(128, 128, 128)');
      expect(ratio).toBeCloseTo(1, 1);
    });

    it('should return 0 for invalid color format', () => {
      const ratio = getContrastRatio('invalid', 'rgb(255, 255, 255)');
      expect(ratio).toBe(0);
    });

    it('should work regardless of color order', () => {
      const ratio1 = getContrastRatio('rgb(0, 0, 0)', 'rgb(255, 255, 255)');
      const ratio2 = getContrastRatio('rgb(255, 255, 255)', 'rgb(0, 0, 0)');
      expect(ratio1).toEqual(ratio2);
    });

    it('should meet WCAG AA standard for normal text (4.5:1)', () => {
      const ratio = getContrastRatio('rgb(0, 0, 0)', 'rgb(255, 255, 255)');
      expect(ratio).toBeGreaterThanOrEqual(4.5);
    });
  });

  describe('validateAriaAttributes', () => {
    it('should detect missing button type', () => {
      const button = document.createElement('button');
      const warnings = validateAriaAttributes(button);
      expect(warnings.some((w) => w.includes('type'))).toBe(true);
    });

    it('should not warn for button with type', () => {
      const button = document.createElement('button');
      button.setAttribute('type', 'button');
      const warnings = validateAriaAttributes(button);
      expect(warnings.some((w) => w.includes('type'))).toBe(false);
    });

    it('should detect missing image alt text', () => {
      const img = document.createElement('img');
      img.src = 'test.jpg';
      const warnings = validateAriaAttributes(img);
      expect(warnings.some((w) => w.includes('alt'))).toBe(true);
    });

    it('should not warn for image with alt', () => {
      const img = document.createElement('img');
      img.setAttribute('alt', 'Test image');
      const warnings = validateAriaAttributes(img);
      expect(warnings.some((w) => w.includes('alt'))).toBe(false);
    });

    it('should detect missing tabindex on custom button', () => {
      const div = document.createElement('div');
      div.setAttribute('role', 'button');
      const warnings = validateAriaAttributes(div);
      expect(warnings.some((w) => w.includes('tabindex'))).toBe(true);
    });

    it('should detect form fields not associated with labels', () => {
      const input = document.createElement('input');
      const warnings = validateAriaAttributes(input);
      expect(warnings.some((w) => w.includes('label'))).toBe(true);
    });

    it('should not warn for form fields associated with labels', () => {
      const label = document.createElement('label');
      const input = document.createElement('input');
      label.appendChild(input);
      const warnings = validateAriaAttributes(input);
      expect(warnings.some((w) => w.includes('label'))).toBe(false);
    });
  });

  describe('initializeAccessibility', () => {
    it('should call setup functions on initialization', () => {
      const setupSpy = jest.spyOn(document, 'querySelectorAll');

      initializeAccessibility();

      expect(setupSpy).toHaveBeenCalled();
    });

    it('should enable keyboard navigation for custom buttons', () => {
      const customButton = document.createElement('div');
      customButton.setAttribute('role', 'button');
      document.body.appendChild(customButton);

      initializeAccessibility();

      const event = new KeyboardEvent('keydown', { key: 'Enter' });
      customButton.dispatchEvent(event);

      customButton.remove();
    });

    it('should validate heading hierarchy', () => {
      const h1 = document.createElement('h1');
      const h3 = document.createElement('h3');
      document.body.appendChild(h1);
      document.body.appendChild(h3);

      const consoleSpy = jest.spyOn(console, 'warn').mockImplementation();

      initializeAccessibility();

      h1.remove();
      h3.remove();
      consoleSpy.mockRestore();
    });
  });
});
