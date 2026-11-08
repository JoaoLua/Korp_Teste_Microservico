import { FormControl } from '@angular/forms';
import { INT_MAX, integerRange, trimmedRequired } from './validators';

describe('Validações dos formulários', () => {
  it('aceita texto com conteúdo e rejeita espaços em branco', () => {
    expect(trimmedRequired(new FormControl('  Produto  '))).toBeNull();
    expect(trimmedRequired(new FormControl('  \t\n '))).toEqual({ required: true });
    expect(trimmedRequired(new FormControl(null))).toEqual({ required: true });
  });

  it('aceita saldo zero e o limite do backend, rejeitando frações e valores fora do intervalo', () => {
    const validate = integerRange(0, INT_MAX);
    expect(validate(new FormControl(0))).toBeNull();
    expect(validate(new FormControl(INT_MAX))).toBeNull();
    expect(validate(new FormControl(-1))).toEqual({ min: { min: 0, actual: -1 } });
    expect(validate(new FormControl(INT_MAX + 1))).toEqual({
      max: { max: INT_MAX, actual: INT_MAX + 1 },
    });
    expect(validate(new FormControl(1.5))).toEqual({ integer: true });
  });

  it('exige quantidade inteira positiva e rejeita ausência ou número recebido como texto', () => {
    const validate = integerRange(1, INT_MAX);
    expect(validate(new FormControl(1))).toBeNull();
    expect(validate(new FormControl(0))).toEqual({ min: { min: 1, actual: 0 } });
    expect(validate(new FormControl(null))).toEqual({ required: true });
    expect(validate(new FormControl('1'))).toEqual({ integer: true });
    expect(validate(new FormControl(Number.NaN))).toEqual({ integer: true });
  });
});
