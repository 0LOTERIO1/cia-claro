import type { CustomerDto } from '../types/api'

interface Props {
  customer: CustomerDto | null
}

export function CustomerInfo({ customer }: Props) {
  return (
    <section className="panel">
      <h2>Cliente</h2>
      <dl>
        <div>
          <dt>Nome</dt>
          <dd>{customer?.name ?? '...'}</dd>
        </div>
        <div>
          <dt>Customer ID</dt>
          <dd>{customer?.id ?? '...'}</dd>
        </div>
        <div>
          <dt>Telefone</dt>
          <dd>{customer?.phone ?? '...'}</dd>
        </div>
      </dl>
    </section>
  )
}
