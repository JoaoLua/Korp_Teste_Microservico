import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

export const INT_MAX = 2_147_483_647;

export function trimmedRequired(control: AbstractControl): ValidationErrors | null {
  return typeof control.value === 'string' && control.value.trim().length > 0
    ? null
    : { required: true };
}

export function integerRange(min: number, max: number): ValidatorFn {
  return (control) => {
    const value: unknown = control.value;
    if (value === null || value === undefined || value === '') return { required: true };
    if (typeof value !== 'number' || !Number.isSafeInteger(value)) return { integer: true };
    if (value < min) return { min: { min, actual: value } };
    if (value > max) return { max: { max, actual: value } };
    return null;
  };
}
