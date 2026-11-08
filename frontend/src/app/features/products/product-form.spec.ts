import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { Subject, of, throwError } from 'rxjs';
import { ProductApi } from './product-api';
import { ProductForm } from './product-form';
import { Product } from './product.models';

const product: Product = {
  id: 'b24a7ab4-c18f-42aa-9496-234c743f88c1',
  code: 'P-01',
  description: 'Produto de teste',
  balance: 10,
  minimumStock: 2,
  category: 'Categoria existente',
  createdAt: '2026-09-09T12:00:00Z',
  updatedAt: null,
  rowVersion: 'AAAAAAAAAAE=',
};

function setup(editing = false) {
  const api = {
    get: vi.fn().mockReturnValue(of(product)),
    create: vi.fn().mockReturnValue(of(product)),
    update: vi.fn().mockReturnValue(of(product)),
    adjust: vi.fn().mockReturnValue(of({ ...product, balance: 7, rowVersion: 'AAAAAAAAAAI=' })),
    improveDescription: vi.fn().mockReturnValue(of({ description: 'Descrição revisada do produto' })),
  };
  TestBed.configureTestingModule({
    imports: [ProductForm],
    providers: [
      provideRouter([]),
      { provide: ProductApi, useValue: api },
      {
        provide: ActivatedRoute,
        useValue: { snapshot: { paramMap: convertToParamMap(editing ? { id: product.id } : {}) } },
      },
    ],
  });
  const fixture = TestBed.createComponent(ProductForm);
  const component = fixture.componentInstance;
  const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
  component.ngOnInit();
  return { component, api, navigate };
}

describe('ProductForm', () => {
  it('apresenta uma sugestão sem alterar o formulário nem salvar o produto', () => {
    const { component, api } = setup();
    component.form.controls.description.setValue(' Descrição original ');

    component.improveDescription();

    expect(api.improveDescription).toHaveBeenCalledWith('Descrição original');
    expect(component.descriptionSuggestion()).toBe('Descrição revisada do produto');
    expect(component.form.controls.description.value).toBe(' Descrição original ');
    expect(component.improvingDescription()).toBe(false);
    expect(api.create).not.toHaveBeenCalled();
    expect(api.update).not.toHaveBeenCalled();
  });

  it('aplica a sugestão somente ao formulário e a salva após confirmação', () => {
    const { component, api } = setup();
    component.form.setValue({ code: 'P-01', description: 'Descrição original', balance: 0 });
    component.improveDescription();

    component.useDescriptionSuggestion();

    expect(component.form.controls.description.value).toBe('Descrição revisada do produto');
    expect(component.form.controls.description.dirty).toBe(true);
    expect(component.descriptionSuggestion()).toBeNull();
    expect(api.create).not.toHaveBeenCalled();
    component.save();
    expect(api.create).toHaveBeenCalledWith({
      code: 'P-01',
      description: 'Descrição revisada do produto',
      balance: 0,
    });
  });

  it('descarta a sugestão preservando a descrição original', () => {
    const { component, api } = setup(true);
    component.improveDescription();

    component.discardDescriptionSuggestion();
    component.useDescriptionSuggestion();

    expect(component.descriptionSuggestion()).toBeNull();
    expect(component.form.controls.description.value).toBe(product.description);
    expect(api.update).not.toHaveBeenCalled();
  });

  it('invalida sugestões após uma edição e ignora respostas para textos já alterados', () => {
    const { component, api } = setup(true);
    component.improveDescription();
    component.form.controls.description.setValue('Minha nova descrição');
    expect(component.descriptionSuggestion()).toBeNull();
    component.useDescriptionSuggestion();
    expect(component.form.controls.description.value).toBe('Minha nova descrição');

    const pending = new Subject<{ description: string }>();
    api.improveDescription.mockReturnValue(pending);
    component.improveDescription();
    component.form.controls.description.setValue('Outra edição');
    component.form.controls.description.setValue('Minha nova descrição');
    pending.next({ description: 'Sugestão para uma edição anterior' });
    pending.complete();

    expect(component.descriptionSuggestion()).toBeNull();
    expect(component.form.controls.description.value).toBe('Minha nova descrição');
    expect(component.improvingDescription()).toBe(false);
  });

  it('permite salvar a descrição original após falha na geração', () => {
    const { component, api } = setup();
    component.form.setValue({ code: 'P-01', description: 'Descrição original', balance: 0 });
    api.improveDescription.mockReturnValue(
      throwError(
        () => new HttpErrorResponse({ status: 503, error: { title: 'Serviço indisponível.' } }),
      ),
    );

    component.improveDescription();

    expect(component.descriptionImprovementError()).toBe('Serviço indisponível.');
    expect(component.form.controls.description.value).toBe('Descrição original');
    expect(component.error()).toBeNull();
    expect(component.needsReload()).toBe(false);
    expect(component.creationUncertain()).toBe(false);
    expect(component.improvingDescription()).toBe(false);
    expect(api.improveDescription).toHaveBeenCalledOnce();
    component.save();
    expect(api.create).toHaveBeenCalledWith({
      code: 'P-01',
      description: 'Descrição original',
      balance: 0,
    });
  });

  it('impede pedidos duplicados de melhoria e mantém o campo editável', () => {
    const { component, api } = setup(true);
    const pending = new Subject<{ description: string }>();
    api.improveDescription.mockReturnValue(pending);

    component.improveDescription();
    component.improveDescription();

    expect(api.improveDescription).toHaveBeenCalledOnce();
    expect(component.improvingDescription()).toBe(true);
    expect(component.form.controls.description.enabled).toBe(true);
    pending.next({ description: 'Descrição revisada' });
    pending.complete();
    expect(component.improvingDescription()).toBe(false);
  });

  it.each(['   ', 'a'.repeat(201)])(
    'rejeita descrição de entrada ou sugestão fora do limite de 1 a 200 caracteres',
    (invalidDescription) => {
      const { component, api } = setup();
      component.form.controls.description.setValue(invalidDescription);
      component.improveDescription();
      expect(api.improveDescription).not.toHaveBeenCalled();

      component.form.controls.description.setValue('Descrição original');
      api.improveDescription.mockReturnValue(of({ description: invalidDescription }));
      component.improveDescription();
      expect(component.descriptionSuggestion()).toBeNull();
      expect(component.descriptionImprovementError()).toContain('descrição válida');
      expect(component.form.controls.description.value).toBe('Descrição original');
    },
  );

  it('rejeita textos vazios e saldo fracionado sem enviar o cadastro', () => {
    const { component, api } = setup();
    component.form.setValue({ code: '   ', description: 'Produto', balance: 1.5 });
    component.save();
    expect(component.form.controls.code.invalid).toBe(true);
    expect(component.form.controls.balance.invalid).toBe(true);
    expect(api.create).not.toHaveBeenCalled();
  });

  it('remove espaços externos e permite cadastrar com saldo zero', () => {
    const { component, api, navigate } = setup();
    component.form.setValue({ code: ' P-01 ', description: ' Produto de teste ', balance: 0 });
    component.save();
    expect(api.create).toHaveBeenCalledWith({
      code: 'P-01',
      description: 'Produto de teste',
      balance: 0,
    });
    expect(navigate).toHaveBeenCalledWith(['/produtos']);
  });

  it('preserva a versão e os campos adicionais na edição, sem enviar saldo', () => {
    const { component, api } = setup(true);
    component.form.patchValue({ description: 'Nova descrição', balance: 999 });
    component.save();
    expect(api.update).toHaveBeenCalledWith(product.id, {
      code: product.code,
      description: 'Nova descrição',
      rowVersion: product.rowVersion,
      minimumStock: product.minimumStock,
      category: product.category,
    });
    expect(api.update.mock.calls[0][1]).not.toHaveProperty('balance');
  });

  it('envia apenas uma requisição enquanto o cadastro está em andamento', () => {
    const { component, api } = setup();
    const pending = new Subject<Product>();
    api.create.mockReturnValue(pending);
    component.form.setValue({ code: 'P-01', description: 'Produto de teste', balance: 10 });
    component.save();
    component.save();
    expect(api.create).toHaveBeenCalledTimes(1);
    expect(component.saving()).toBe(true);
    pending.next(product);
    pending.complete();
    expect(component.saving()).toBe(false);
  });

  it('preserva o formulário e exige recarga explícita após conflito de versão', () => {
    const { component, api } = setup(true);
    api.update.mockReturnValue(
      throwError(
        () =>
          new HttpErrorResponse({
            status: 409,
            error: { code: 'ROW_VERSION_CONFLICT', title: 'O produto foi alterado.' },
          }),
      ),
    );
    component.form.controls.description.setValue('Minha alteração');
    component.save();
    component.save();
    expect(component.form.controls.description.value).toBe('Minha alteração');
    expect(component.needsReload()).toBe(true);
    expect(api.update).toHaveBeenCalledTimes(1);
    expect(api.get).toHaveBeenCalledTimes(1);

    api.get.mockReturnValue(
      of({ ...product, description: 'Alteração remota', rowVersion: 'AAAAAAAAAAM=' }),
    );
    component.reload();
    expect(component.needsReload()).toBe(false);
    expect(component.form.controls.description.value).toBe('Alteração remota');
    expect(component.product()?.rowVersion).toBe('AAAAAAAAAAM=');
  });

  it('usa a nova versão após ajuste de saldo e conserva alterações ainda não salvas', () => {
    const { component, api } = setup(true);
    component.form.controls.description.setValue('Descrição em edição');
    component.adjustmentForm.setValue({ balance: 7, reason: ' Contagem física ' });
    component.adjust();
    expect(api.adjust).toHaveBeenCalledWith(product.id, {
      balance: 7,
      reason: 'Contagem física',
      rowVersion: product.rowVersion,
    });
    expect(component.form.controls.description.value).toBe('Descrição em edição');
    expect(component.product()?.balance).toBe(7);
    component.save();
    expect(api.update).toHaveBeenCalledWith(
      product.id,
      expect.objectContaining({ description: 'Descrição em edição', rowVersion: 'AAAAAAAAAAI=' }),
    );
  });

  it.each([0, 500])('bloqueia novo cadastro após resultado incerto com status %s', (status) => {
    const { component, api } = setup();
    api.create.mockReturnValue(throwError(() => new HttpErrorResponse({ status })));
    component.form.setValue({ code: 'P-01', description: 'Produto de teste', balance: 10 });
    component.save();
    component.save();
    expect(api.create).toHaveBeenCalledTimes(1);
    expect(component.form.controls.code.value).toBe('P-01');
    expect(component.error()).toContain('Consulte a lista de produtos');
    expect(component.saving()).toBe(false);
    expect(component.creationUncertain()).toBe(true);
  });

  it.each(['update', 'adjust'] as const)(
    'exige recarga após falha 503 em %s antes de repetir a alteração',
    (operation) => {
      const { component, api } = setup(true);
      api[operation].mockReturnValue(throwError(() => new HttpErrorResponse({ status: 503 })));
      component.form.controls.description.setValue('Descrição alterada');
      component.adjustmentForm.setValue({ balance: 7, reason: 'Contagem física' });
      const submit = () => (operation === 'update' ? component.save() : component.adjust());

      submit();
      submit();

      expect(api[operation]).toHaveBeenCalledTimes(1);
      expect(component.needsReload()).toBe(true);
      expect(component.error()).toContain('Recarregue o produto');
      expect(component.creationUncertain()).toBe(false);

      const confirmedProduct = { ...product, balance: 7, rowVersion: 'AAAAAAAAAAI=' };
      api.get.mockReturnValue(of(confirmedProduct));
      component.reload();
      expect(component.needsReload()).toBe(false);
      expect(component.product()).toEqual(confirmedProduct);
    },
  );
});
