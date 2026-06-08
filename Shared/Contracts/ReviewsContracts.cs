using MediatR;
using System;
using System.Collections.Generic;

namespace Contracts;

public record GetCustomerByExternalIdQuery(string ExternalUserId) : IRequest<Guid?>;

public record VerifyBuyerQuery(Guid CustomerId, Guid ProductId, string? CustomerEmail = null) : IRequest<bool>;

public record GetDeliveredProductIdsQuery(Guid CustomerId) : IRequest<List<Guid>>;

public record GetFrequentlyBoughtTogetherProductIdsQuery(Guid ProductId, int Limit) : IRequest<List<Guid>>;
