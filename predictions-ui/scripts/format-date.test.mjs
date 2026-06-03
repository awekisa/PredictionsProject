import { describe, it } from 'node:test';
import assert from 'node:assert/strict';
import { formatDateTime, formatTime } from '../src/utils/formatDate.ts';

describe('date/time formatting', () => {
  it('formats explicit UTC API instants in the device timezone', () => {
    assert.equal(formatDateTime('2026-06-11T19:00:00Z'), '11/06/2026 22:00');
    assert.equal(formatTime('2026-06-11T19:00:00Z'), '22:00');
  });

  it('treats offset-less API timestamps as UTC before formatting locally', () => {
    assert.equal(formatDateTime('2026-06-11T19:00:00'), '11/06/2026 22:00');
    assert.equal(formatTime('2026-06-11T19:00:00'), '22:00');
  });
});
