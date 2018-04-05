﻿using System;
using System.Threading.Tasks;
using AutoMapper;
using Duber.Domain.Trip.Events;
using Duber.Infrastructure.EventBus.Abstractions;
using Duber.Trip.API.Application.IntegrationEvents;
using Duber.Trip.API.Application.Model;
using Weapsy.Cqrs.Events;
using TripStatus = Duber.Domain.Trip.Model.TripStatus;

namespace Duber.Trip.API.Application.DomainEventHandlers
{
    public class TripUpdatedDomainEventHandlerAsync : IEventHandlerAsync<TripUpdatedDomainEvent>
    {
        private readonly IEventBus _eventBus;
        private readonly IMapper _mapper;

        public TripUpdatedDomainEventHandlerAsync(IEventBus eventBus, IMapper mapper)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }

        public async Task HandleAsync(TripUpdatedDomainEvent @event)
        {
            var integrationEvent = _mapper.Map<TripUpdatedIntegrationEvent>(@event);

            // to update the query side (materialized view)
            _eventBus.Publish(integrationEvent); // TODO: make an async Publish method.

            // events for invoice microservice
            if (@event.Status.Name == TripStatus.Finished.Name)
            {
                if (!@event.Distance.HasValue || !@event.Duration.HasValue)
                    throw new ArgumentException("Distance and duration are required to trigger a TripFinishedIntegrationEvent");

                _eventBus.Publish(new TripFinishedIntegrationEvent(
                    @event.AggregateRootId,
                    @event.Distance.Value,
                    @event.Duration.Value,
                    new PaymentMethod { Id = @event.PaymentMethod.Id, Name = @event.PaymentMethod.Name }));
            }
            else if (@event.Status.Name == TripStatus.Cancelled.Name)
            {
                if (!@event.Started.HasValue)
                    throw new ArgumentException("Started trip time is required to trigger a TripCancelledIntegrationEvent");

                _eventBus.Publish(new TripCancelledIntegrationEvent(
                    @event.AggregateRootId,
                    @event.Started.Value,
                    new PaymentMethod { Id = @event.PaymentMethod.Id, Name = @event.PaymentMethod.Name }));
            }

            await Task.CompletedTask;
        }
    }
}
