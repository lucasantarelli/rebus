﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Rebus.Exceptions;
using Rebus.Messages;
using Rebus.Messages.Control;
using Rebus.Pipeline;

namespace Rebus.Legacy
{
    [StepDocumentation("Unpacks the object[] that is always the root object in a legacy message.")]
    class UnpackLegacyMessageIncomingStep : IIncomingStep
    {
        public async Task Process(IncomingStepContext context, Func<Task> next)
        {
            var message = context.Load<Message>();
            var headers = message.Headers;

            if (headers.ContainsKey(MapLegacyHeadersIncomingStep.LegacyMessageHeader))
            {
                var body = message.Body;
                var array = body as object[];

                if (array == null)
                {
                    throw new FormatException(string.Format("Incoming message has the '{0}' header, but the message body {1} is not an object[] as expected",
                        MapLegacyHeadersIncomingStep.LegacyMessageHeader, body));
                }

                if (array.Length != 1)
                {
                    throw new FormatException(string.Format("Incoming message has the '{0}' header, and the message body is an object[] as expected, but the array has {1} elements - the legacy unpacker can only work with one single logical message in each transport message, sorry",
                        MapLegacyHeadersIncomingStep.LegacyMessageHeader, array.Length));
                }

                var messageBodyToDispatch = PossiblyConvertBody(array[0], headers);

                context.Save(new Message(headers, messageBodyToDispatch));
            }

            await next();
        }

        static object PossiblyConvertBody(object messageBody, IReadOnlyDictionary<string, string> headers)
        {
            var legacySubscriptionMessage = messageBody as LegacySubscriptionMessageSerializer.LegacySubscriptionMessage;

            if (legacySubscriptionMessage == null)
            {
                return messageBody;
            }

            string returnAddress;
            var topic = legacySubscriptionMessage.Type;

            if (!headers.TryGetValue(Headers.ReturnAddress, out returnAddress))
            {
                throw new RebusApplicationException(
                    "Got legacy subscription message but the '{0}' header was not present on it!", Headers.ReturnAddress);
            }

            var subscribe = legacySubscriptionMessage.Action == 0;

            if (subscribe)
            {
                return new SubscribeRequest
                {
                    Topic = topic,
                    SubscriberAddress = returnAddress
                };
            }

            return new UnsubscribeRequest
            {
                Topic = topic,
                SubscriberAddress = returnAddress
            };
        }
    }
}