import { describe, it } from 'node:test';
import assert from 'node:assert/strict';
import { toUtcIsoString, toDatetimeLocalValue, isStarted } from '../src/utils/localTime.ts';

describe('local/UTC time helpers', () => {
  it('serializes datetime-local values as UTC instants', () => {
    assert.equal(toUtcIsoString('2026-06-11T22:00'), '2026-06-11T19:00:00.000Z');
  });

  it('formats UTC API values for datetime-local inputs', () => {
    assert.equal(toDatetimeLocalValue('2026-06-11T19:00:00Z'), '2026-06-11T22:00');
    assert.equal(toDatetimeLocalValue('2026-06-11T19:00:00'), '2026-06-11T22:00');
  });

  it('compares prediction deadlines by instant', () => {
    assert.equal(isStarted('2026-06-11T19:00:00Z', new Date('2026-06-11T18:59:59Z')), false);
    assert.equal(isStarted('2026-06-11T19:00:00Z', new Date('2026-06-11T19:00:00Z')), true);
  });
});
