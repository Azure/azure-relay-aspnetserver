// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http.Features;

namespace Microsoft.Azure.Relay.AspNetCore
{
    internal sealed class StandardFeatureCollection : IFeatureCollection
    {
        private static readonly Func<FeatureContext, object> _identityFunc = ReturnIdentity;
        private static readonly Dictionary<Type, Func<FeatureContext, object>> _featureFuncLookup = new Dictionary<Type, Func<FeatureContext, object>>()
        {
            { typeof(IHttpRequestFeature), _identityFunc },
            { typeof(IHttpConnectionFeature), _identityFunc },
            { typeof(IHttpResponseFeature), _identityFunc },
            { typeof(IHttpBufferingFeature), _identityFunc },            
            { typeof(IHttpRequestIdentifierFeature), _identityFunc },
            { typeof(IHttpSendFileFeature), _identityFunc },
            { typeof(IHttpUpgradeFeature), _identityFunc },
            { typeof(IHttpWebSocketFeature), _identityFunc },
            { typeof(RelayedHttpListenerContext), ctx => ctx.RequestContext }
        };

        private readonly FeatureContext _featureContext;

        static StandardFeatureCollection()
        {
            // TODO: CR: Isn't this added above?
            _featureFuncLookup[typeof(IHttpUpgradeFeature)] = _identityFunc;
        }

        public StandardFeatureCollection(FeatureContext featureContext)
        {
            _featureContext = featureContext;
        }

        public bool IsReadOnly
        {
            get { return true; }
        }

        public int Revision
        {
            get { return 0; }
        }

        public object this[Type key]
        {
            get
            {
                Func<FeatureContext, object> lookupFunc;
                _featureFuncLookup.TryGetValue(key, out lookupFunc);
                return lookupFunc?.Invoke(_featureContext);
            }
            set
            {
                throw new InvalidOperationException("The collection is read-only");
            }
        }

        private static object ReturnIdentity(FeatureContext featureContext)
        {
            return featureContext;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<KeyValuePair<Type, object>>)this).GetEnumerator();
        }

        IEnumerator<KeyValuePair<Type, object>> IEnumerable<KeyValuePair<Type, object>>.GetEnumerator()
        {
            foreach (var featureFunc in _featureFuncLookup)
            {
                var feature = featureFunc.Value(_featureContext);
                if (feature != null)
                {
                    yield return new KeyValuePair<Type, object>(featureFunc.Key, feature);
                }
            }
        }

        public TFeature Get<TFeature>()
        {
            return (TFeature)this[typeof(TFeature)];
        }

        public void Set<TFeature>(TFeature instance)
        {
            this[typeof(TFeature)] = instance;
        }
    }
}
