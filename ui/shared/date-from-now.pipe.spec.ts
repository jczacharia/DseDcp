import {DateFromNowPipe} from '#shared/date-from-now.pipe';
import {render, screen} from '@testing-library/angular';
import {afterAll, beforeAll, describe, expect, it, vi} from 'vitest';

describe(DateFromNowPipe.name, () => {
  const NOW = new Date('2026-04-30T12:00:00Z');

  beforeAll(() => {
    vi.useFakeTimers();
    vi.setSystemTime(NOW);
  });

  afterAll(() => {
    vi.useRealTimers();
  });

  describe('null / empty input', () => {
    it('renders an empty string for null', async () => {
      await render(`<span data-testid="out">{{ value | dateFromNow }}</span>`, {
        imports: [DateFromNowPipe],
        componentProperties: {value: null},
      });
      expect(screen.getByTestId('out')).toHaveTextContent('');
    });

    it('renders an empty string for an empty string input', async () => {
      await render(`<span data-testid="out">{{ value | dateFromNow }}</span>`, {
        imports: [DateFromNowPipe],
        componentProperties: {value: ''},
      });
      expect(screen.getByTestId('out')).toHaveTextContent('');
    });
  });

  describe('default options', () => {
    it('formats a past Date without a suffix', async () => {
      const fiveMinutesAgo = new Date(NOW.getTime() - 5 * 60 * 1000);
      await render(`<span data-testid="out">{{ value | dateFromNow }}</span>`, {
        imports: [DateFromNowPipe],
        componentProperties: {value: fiveMinutesAgo},
      });
      expect(screen.getByTestId('out')).toHaveTextContent('5 minutes');
    });

    it('formats a future Date without an "in" prefix', async () => {
      const inTenMinutes = new Date(NOW.getTime() + 10 * 60 * 1000);
      await render(`<span data-testid="out">{{ value | dateFromNow }}</span>`, {
        imports: [DateFromNowPipe],
        componentProperties: {value: inTenMinutes},
      });
      expect(screen.getByTestId('out')).toHaveTextContent('10 minutes');
    });

    it('accepts an ISO string input', async () => {
      const twoHoursAgo = new Date(NOW.getTime() - 2 * 60 * 60 * 1000).toISOString();
      await render(`<span data-testid="out">{{ value | dateFromNow }}</span>`, {
        imports: [DateFromNowPipe],
        componentProperties: {value: twoHoursAgo},
      });
      expect(screen.getByTestId('out')).toHaveTextContent('2 hours');
    });

    it('renders "0 seconds" for the current instant', async () => {
      await render(`<span data-testid="out">{{ value | dateFromNow }}</span>`, {
        imports: [DateFromNowPipe],
        componentProperties: {value: NOW},
      });
      expect(screen.getByTestId('out')).toHaveTextContent('0 seconds');
    });
  });

  describe('options argument', () => {
    it('adds a suffix when addSuffix is true', async () => {
      const oneHourAgo = new Date(NOW.getTime() - 60 * 60 * 1000);
      await render(`<span data-testid="out">{{ value | dateFromNow: {addSuffix: true} }}</span>`, {
        imports: [DateFromNowPipe],
        componentProperties: {value: oneHourAgo},
      });
      expect(screen.getByTestId('out')).toHaveTextContent('1 hour ago');
    });

    it('honors the unit option', async () => {
      const oneHourAgo = new Date(NOW.getTime() - 60 * 60 * 1000);
      await render(`<span data-testid="out">{{ value | dateFromNow: {unit: 'minute'} }}</span>`, {
        imports: [DateFromNowPipe],
        componentProperties: {value: oneHourAgo},
      });
      expect(screen.getByTestId('out')).toHaveTextContent('60 minutes');
    });

    it('honors the roundingMethod option', async () => {
      const almostTwoMinutes = new Date(NOW.getTime() - 110 * 1000);
      const {rerender} = await render(`<span data-testid="out">{{ value | dateFromNow:opts }}</span>`, {
        imports: [DateFromNowPipe],
        componentProperties: {value: almostTwoMinutes, opts: {roundingMethod: 'floor'}},
      });
      expect(screen.getByTestId('out')).toHaveTextContent('1 minute');

      await rerender({componentProperties: {value: almostTwoMinutes, opts: {roundingMethod: 'ceil'}}});
      expect(screen.getByTestId('out')).toHaveTextContent('2 minutes');
    });
  });
});
