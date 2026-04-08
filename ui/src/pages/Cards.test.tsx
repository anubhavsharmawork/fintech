import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { BrowserRouter } from 'react-router-dom';
import Cards from './Cards';
import { ToastProvider } from '../components/Toast';
import * as cardService from '../services/cardService';

jest.mock('../services/cardService');
jest.mock('../context/AppContext', () => ({
  useAppContext: jest.fn(),
}));
import * as AppContext from '../context/AppContext';

const mockCards: cardService.VirtualCard[] = [
  {
    id: 'card1',
    userId: 'user1',
    nickname: 'Travel Card',
    last4: '4242',
    expiryMonth: 12,
    expiryYear: 2027,
    status: 'active',
    createdAt: '2024-01-01T00:00:00Z',
  },
  {
    id: 'card2',
    userId: 'user1',
    nickname: 'Shopping Card',
    last4: '1234',
    expiryMonth: 6,
    expiryYear: 2026,
    status: 'frozen',
    createdAt: '2024-01-02T00:00:00Z',
  },
];

describe('Cards Page', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    (AppContext.useAppContext as jest.Mock).mockReturnValue({ user: null });
    (cardService.listCards as jest.Mock).mockResolvedValue(mockCards);
    (cardService.createCard as jest.Mock).mockResolvedValue({
      card: { ...mockCards[0], id: 'card3', nickname: 'New Card', last4: '9999' },
      cardNumber: '4111111111119999',
      cvv: '321',
    });
    (cardService.freezeCard as jest.Mock).mockResolvedValue({ ...mockCards[0], status: 'frozen' });
    (cardService.unfreezeCard as jest.Mock).mockResolvedValue({ ...mockCards[1], status: 'active' });
    (cardService.deleteCard as jest.Mock).mockResolvedValue(undefined);
  });

  const renderComponent = () =>
    render(
      <BrowserRouter>
        <ToastProvider>
          <Cards />
        </ToastProvider>
      </BrowserRouter>,
    );

  describe('initial render', () => {
    it('shows Virtual Cards heading', async () => {
      renderComponent();
      await waitFor(() => {
        expect(screen.getByText('Virtual Cards')).toBeInTheDocument();
      });
    });

    it('shows + New Card button', async () => {
      renderComponent();
      await waitFor(() => {
        expect(screen.getByText('+ New Card')).toBeInTheDocument();
      });
    });

    it('calls listCards on mount', async () => {
      renderComponent();
      await waitFor(() => {
        expect(cardService.listCards).toHaveBeenCalledTimes(1);
      });
    });

    it('renders card nicknames', async () => {
      renderComponent();
      await waitFor(() => {
        expect(screen.getByText('Travel Card')).toBeInTheDocument();
        expect(screen.getByText('Shopping Card')).toBeInTheDocument();
      });
    });

    it('renders masked card numbers', async () => {
      renderComponent();
      await waitFor(() => {
        expect(screen.getByText(/•••• •••• •••• 4242/)).toBeInTheDocument();
        expect(screen.getByText(/•••• •••• •••• 1234/)).toBeInTheDocument();
      });
    });

    it('displays frozen badge on frozen card', async () => {
      renderComponent();
      await waitFor(() => {
        expect(screen.getByText('Frozen')).toBeInTheDocument();
      });
    });

    it('formats expiry correctly', async () => {
      renderComponent();
      await waitFor(() => {
        expect(screen.getByText('12 / 2027')).toBeInTheDocument();
        expect(screen.getByText('06 / 2026')).toBeInTheDocument();
      });
    });
  });

  describe('load error', () => {
    it('shows error toast when listCards fails', async () => {
      (cardService.listCards as jest.Mock).mockRejectedValue(new Error('Failed to load cards'));
      renderComponent();
      await waitFor(() => {
        expect(screen.getByText('Failed to load cards')).toBeInTheDocument();
      });
    });
  });

  describe('create card', () => {
    it('toggles create form visibility', async () => {
      renderComponent();
      await waitFor(() => expect(screen.getByText('+ New Card')).toBeInTheDocument());

      fireEvent.click(screen.getByText('+ New Card'));

      await waitFor(() => {
        expect(screen.getByText('Create Virtual Card')).toBeInTheDocument();
        expect(screen.getByText('Cancel')).toBeInTheDocument();
      });
    });

    it('dismisses form on Cancel', async () => {
      renderComponent();
      await waitFor(() => expect(screen.getByText('+ New Card')).toBeInTheDocument());
      fireEvent.click(screen.getByText('+ New Card'));
      await waitFor(() => expect(screen.getByText('Create Virtual Card')).toBeInTheDocument());

      fireEvent.click(screen.getByText('Cancel'));
      await waitFor(() => {
        expect(screen.queryByText('Create Virtual Card')).not.toBeInTheDocument();
      });
    });

    it('shows nickname input in form', async () => {
      renderComponent();
      await waitFor(() => expect(screen.getByText('+ New Card')).toBeInTheDocument());
      fireEvent.click(screen.getByText('+ New Card'));
      await waitFor(() => {
        expect(screen.getByLabelText('Card Nickname')).toBeInTheDocument();
      });
    });

    it('creates a card on submit', async () => {
      renderComponent();
      await waitFor(() => expect(screen.getByText('+ New Card')).toBeInTheDocument());
      fireEvent.click(screen.getByText('+ New Card'));
      await waitFor(() => expect(screen.getByLabelText('Card Nickname')).toBeInTheDocument());

      fireEvent.change(screen.getByLabelText('Card Nickname'), { target: { value: 'New Card' } });
      fireEvent.submit(screen.getByLabelText('Card Nickname').closest('form')!);

      await waitFor(() => {
        expect(cardService.createCard).toHaveBeenCalledWith('New Card');
      });
    });

    it('shows one-time reveal panel after creation', async () => {
      renderComponent();
      await waitFor(() => expect(screen.getByText('+ New Card')).toBeInTheDocument());
      fireEvent.click(screen.getByText('+ New Card'));
      await waitFor(() => expect(screen.getByLabelText('Card Nickname')).toBeInTheDocument());

      fireEvent.change(screen.getByLabelText('Card Nickname'), { target: { value: 'New Card' } });
      fireEvent.submit(screen.getByLabelText('Card Nickname').closest('form')!);

      await waitFor(() => {
        expect(screen.getByText('4111111111119999')).toBeInTheDocument();
        expect(screen.getByText('321')).toBeInTheDocument();
      });
    });

    it('dismisses reveal panel', async () => {
      renderComponent();
      await waitFor(() => expect(screen.getByText('+ New Card')).toBeInTheDocument());
      fireEvent.click(screen.getByText('+ New Card'));
      await waitFor(() => expect(screen.getByLabelText('Card Nickname')).toBeInTheDocument());
      fireEvent.change(screen.getByLabelText('Card Nickname'), { target: { value: 'New Card' } });
      fireEvent.submit(screen.getByLabelText('Card Nickname').closest('form')!);
      await waitFor(() => expect(screen.getByText('4111111111119999')).toBeInTheDocument());

      fireEvent.click(screen.getByRole('button', { name: /got it/i }));

      await waitFor(() => {
        expect(screen.queryByText('4111111111119999')).not.toBeInTheDocument();
      });
    });

    it('shows error toast on create failure', async () => {
      (cardService.createCard as jest.Mock).mockRejectedValue(new Error('Creation failed'));
      renderComponent();
      await waitFor(() => expect(screen.getByText('+ New Card')).toBeInTheDocument());
      fireEvent.click(screen.getByText('+ New Card'));
      await waitFor(() => expect(screen.getByLabelText('Card Nickname')).toBeInTheDocument());

      fireEvent.change(screen.getByLabelText('Card Nickname'), { target: { value: 'My Card' } });
      fireEvent.submit(screen.getByLabelText('Card Nickname').closest('form')!);

      await waitFor(() => {
        expect(screen.getByText('Creation failed')).toBeInTheDocument();
      });
    });
  });

  describe('freeze / unfreeze', () => {
    it('freeze button calls freezeCard', async () => {
      renderComponent();
      await waitFor(() => expect(screen.getByText('Travel Card')).toBeInTheDocument());

      const freezeBtn = screen.getAllByRole('button', { name: /freeze/i })[0];
      fireEvent.click(freezeBtn);

      await waitFor(() => {
        expect(cardService.freezeCard).toHaveBeenCalledWith('card1');
      });
    });

    it('unfreeze button calls unfreezeCard', async () => {
      renderComponent();
      await waitFor(() => expect(screen.getByText('Shopping Card')).toBeInTheDocument());

      const unfreezeBtn = screen.getByRole('button', { name: /unfreeze/i });
      fireEvent.click(unfreezeBtn);

      await waitFor(() => {
        expect(cardService.unfreezeCard).toHaveBeenCalledWith('card2');
      });
    });

    it('shows toast error on freeze failure', async () => {
      (cardService.freezeCard as jest.Mock).mockRejectedValue(new Error('Failed to freeze card'));
      renderComponent();
      await waitFor(() => expect(screen.getByText('Travel Card')).toBeInTheDocument());

      const freezeBtn = screen.getAllByRole('button', { name: /freeze/i })[0];
      fireEvent.click(freezeBtn);

      await waitFor(() => {
        expect(screen.getByText('Failed to freeze card')).toBeInTheDocument();
      });
    });
  });

  describe('mouse interactions on card visual', () => {
    it('does not update tilt when card is frozen', async () => {
      renderComponent();
      await waitFor(() => expect(screen.getByText('Shopping Card')).toBeInTheDocument());

      const frozenCard = screen.getByText(/•••• •••• •••• 1234/).closest('.vc-card-visual')!;
      fireEvent.mouseMove(frozenCard, { clientX: 10, clientY: 10 });
      // frozen card early-return — style should remain at perspective(600px) rotateX(0deg) rotateY(0deg)
      expect(frozenCard).toHaveStyle('transform: perspective(600px) rotateX(0deg) rotateY(0deg)');
    });

    it('resets tilt on mouse leave', async () => {
      renderComponent();
      await waitFor(() => expect(screen.getByText('Travel Card')).toBeInTheDocument());

      const activeCard = screen.getByText(/•••• •••• •••• 4242/).closest('.vc-card-visual')!;
      fireEvent.mouseLeave(activeCard);
      expect(activeCard).toHaveStyle('transform: perspective(600px) rotateX(0deg) rotateY(0deg)');
    });
  });

  describe('reveal modal dismissal', () => {
    it('dismisses reveal panel by clicking the overlay', async () => {
      renderComponent();
      await waitFor(() => expect(screen.getByText('+ New Card')).toBeInTheDocument());
      fireEvent.click(screen.getByText('+ New Card'));
      await waitFor(() => expect(screen.getByLabelText('Card Nickname')).toBeInTheDocument());
      fireEvent.change(screen.getByLabelText('Card Nickname'), { target: { value: 'New Card' } });
      fireEvent.submit(screen.getByLabelText('Card Nickname').closest('form')!);
      await waitFor(() => expect(screen.getByText('4111111111119999')).toBeInTheDocument());

      // Click the overlay backdrop (not the modal itself)
      const overlay = document.querySelector('.vc-overlay') as HTMLElement;
      fireEvent.click(overlay);

      await waitFor(() => {
        expect(screen.queryByText('4111111111119999')).not.toBeInTheDocument();
      });
    });
  });

  describe('empty nickname submit', () => {
    it('does not call createCard when nickname is blank', async () => {
      renderComponent();
      await waitFor(() => expect(screen.getByText('+ New Card')).toBeInTheDocument());
      fireEvent.click(screen.getByText('+ New Card'));
      await waitFor(() => expect(screen.getByLabelText('Card Nickname')).toBeInTheDocument());

      // Leave nickname empty and submit directly
      fireEvent.submit(screen.getByLabelText('Card Nickname').closest('form')!);

      await waitFor(() => {
        expect(cardService.createCard).not.toHaveBeenCalled();
      });
    });
  });

  describe('delete card', () => {
    it('shows confirm delete dialog on delete click', async () => {
      renderComponent();
      await waitFor(() => expect(screen.getByText('Travel Card')).toBeInTheDocument());

      const deleteBtn = screen.getAllByRole('button', { name: /delete/i })[0];
      fireEvent.click(deleteBtn);

      await waitFor(() => {
        expect(screen.getByText(/are you sure/i)).toBeInTheDocument();
      });
    });

    it('calls deleteCard on confirm', async () => {
      renderComponent();
      await waitFor(() => expect(screen.getByText('Travel Card')).toBeInTheDocument());

      fireEvent.click(screen.getAllByRole('button', { name: /delete/i })[0]);
      await waitFor(() => expect(screen.getByText(/are you sure/i)).toBeInTheDocument());

      fireEvent.click(screen.getByRole('button', { name: /confirm/i }));

      await waitFor(() => {
        expect(cardService.deleteCard).toHaveBeenCalledWith('card1');
      });
    });

    it('does not delete on cancel', async () => {
      renderComponent();
      await waitFor(() => expect(screen.getByText('Travel Card')).toBeInTheDocument());

      fireEvent.click(screen.getAllByRole('button', { name: /delete/i })[0]);
      await waitFor(() => expect(screen.getByText(/are you sure/i)).toBeInTheDocument());

      fireEvent.click(screen.getByRole('button', { name: /cancel/i }));

      await waitFor(() => {
        expect(cardService.deleteCard).not.toHaveBeenCalled();
      });
    });
  });

  describe('cardholder name derivation', () => {
    it('uses user name when available', async () => {
      (AppContext.useAppContext as jest.Mock).mockReturnValue({
        user: { name: 'Alice Smith', email: 'alice@example.com' },
      });
      renderComponent();
      await waitFor(() => {
        expect(screen.getByText('ALICE SMITH')).toBeInTheDocument();
      });
    });

    it('falls back to email when name is absent', async () => {
      (AppContext.useAppContext as jest.Mock).mockReturnValue({
        user: { name: '', email: 'bob@example.com' },
      });
      renderComponent();
      await waitFor(() => {
        expect(screen.getByText('BOB@EXAMPLE.COM')).toBeInTheDocument();
      });
    });

    it('shows empty cardholder when user is null', async () => {
      (AppContext.useAppContext as jest.Mock).mockReturnValue({ user: null });
      renderComponent();
      await waitFor(() => expect(screen.getByText('Travel Card')).toBeInTheDocument());
      // Cardholder value should be an empty string rendered in uppercase (empty)
      const labels = screen.getAllByText('CARDHOLDER');
      expect(labels.length).toBeGreaterThan(0);
    });
  });

  describe('delete card error handling', () => {
    it('shows error toast on delete failure', async () => {
      (cardService.deleteCard as jest.Mock).mockRejectedValue(new Error('Failed to delete card'));
      renderComponent();
      await waitFor(() => expect(screen.getByText('Travel Card')).toBeInTheDocument());

      fireEvent.click(screen.getAllByRole('button', { name: /delete/i })[0]);
      await waitFor(() => expect(screen.getByText(/are you sure/i)).toBeInTheDocument());
      fireEvent.click(screen.getByRole('button', { name: /confirm/i }));

      await waitFor(() => {
        expect(screen.getByText('Failed to delete card')).toBeInTheDocument();
      });
    });
  });

  describe('empty state', () => {
    it('shows empty state when no cards exist', async () => {
      (cardService.listCards as jest.Mock).mockResolvedValue([]);
      renderComponent();
      await waitFor(() => {
        expect(screen.getByText(/no virtual cards yet/i)).toBeInTheDocument();
      });
    });
  });

  describe('unfreeze error handling', () => {
    it('shows error toast on unfreeze failure', async () => {
      (cardService.unfreezeCard as jest.Mock).mockRejectedValue(new Error('Failed to unfreeze card'));
      renderComponent();
      await waitFor(() => expect(screen.getByText('Shopping Card')).toBeInTheDocument());

      const unfreezeBtn = screen.getByRole('button', { name: /unfreeze/i });
      fireEvent.click(unfreezeBtn);

      await waitFor(() => {
        expect(screen.getByText('Failed to unfreeze card')).toBeInTheDocument();
      });
    });
  });
});
