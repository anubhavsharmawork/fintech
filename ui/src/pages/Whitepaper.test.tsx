
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import Whitepaper from './Whitepaper';

describe('Whitepaper', () => {
  beforeEach(() => {
    // Mock IntersectionObserver
    global.IntersectionObserver = jest.fn().mockImplementation(() => ({
      observe: jest.fn(),
      unobserve: jest.fn(),
      disconnect: jest.fn()
    }));
  });

  it('should render the whitepaper with main heading', () => {
    render(<Whitepaper />);

    expect(screen.getByRole('article', { name: /open finance whitepaper/i })).toBeInTheDocument();
  });

  it('should render table of contents toggle button', () => {
    render(<Whitepaper />);

    expect(screen.getByRole('button', { name: /table of contents/i })).toBeInTheDocument();
  });

  it('should toggle TOC menu when button clicked', async () => {
    const user = userEvent.setup();
    render(<Whitepaper />);

    const toggleBtn = screen.getByRole('button', { name: /table of contents/i });
    expect(toggleBtn).toHaveAttribute('aria-expanded', 'false');

    await user.click(toggleBtn);

    expect(toggleBtn).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByText(/close contents/i)).toBeInTheDocument();
  });

  it('should render all major sections', () => {
    render(<Whitepaper />);

    expect(document.getElementById('abstract')).toBeInTheDocument();
    expect(document.getElementById('problem')).toBeInTheDocument();
    expect(document.getElementById('vision')).toBeInTheDocument();
    expect(document.getElementById('architecture')).toBeInTheDocument();
  });

  it('should setup IntersectionObserver on mount', () => {
    render(<Whitepaper />);

    expect(global.IntersectionObserver).toHaveBeenCalled();
  });

  it('should cleanup observers on unmount', () => {
    const disconnectMock = jest.fn();
    global.IntersectionObserver = jest.fn().mockImplementation(() => ({
      observe: jest.fn(),
      unobserve: jest.fn(),
      disconnect: disconnectMock
    }));

    const { unmount } = render(<Whitepaper />);
    unmount();

    expect(disconnectMock).toHaveBeenCalled();
  });
});

