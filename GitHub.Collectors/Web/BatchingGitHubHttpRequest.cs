﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.CloudMine.Core.Collectors.Authentication;
using Microsoft.CloudMine.Core.Collectors.Collector;
using Microsoft.CloudMine.Core.Collectors.Web;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Microsoft.CloudMine.GitHub.Collectors.Web
{
    public class BatchingGitHubHttpRequest : IBatchingHttpRequest
    {
        private static readonly string RelNext = $"rel=\"next\"";

        private readonly GitHubHttpClient httpClient;
        private readonly string apiName;
        private readonly List<HttpResponseSignature> allowlistedResponses;

        public bool HasNext { get; private set; }
        public string CurrentUrl { get; private set; }
        public string PreviousUrl { get; private set; }
        public string PreviousIdentity { get; private set; }

        public BatchingGitHubHttpRequest(GitHubHttpClient httpClient, string initialUrl, string apiName, List<HttpResponseSignature> allowlistedResponses)
        {
            this.httpClient = httpClient;
            this.apiName = apiName;
            this.allowlistedResponses = allowlistedResponses;

            this.HasNext = true;
            this.CurrentUrl = initialUrl;
            this.PreviousUrl = null;
            this.PreviousIdentity = null;
        }

        public async Task<HttpResponseMessage> NextResponseAsync(IAuthentication authentication)
        {
            if (!this.HasNext)
            {
                return null;
            }

            HttpResponseMessage response = await this.httpClient.GetAsync(this.CurrentUrl, authentication, this.apiName, this.allowlistedResponses).ConfigureAwait(false);
            this.HasNext = false;
            this.PreviousUrl = this.CurrentUrl;
            this.PreviousIdentity = authentication.Identity;

            if (response.Headers.TryGetValues("Link", out IEnumerable<string> linkValues))
            {
                string linkValue = linkValues.First();
                string[] links = linkValue.Split(", ");
                foreach (string link in links)
                {
                    string[] parts = link.Split("; ");

                    string urlPart = parts[0];

                    string typePart = parts[1];
                    if (typePart.Equals(RelNext))
                    {
                        // urlPart contains "<" at the beginning and ">" at the end. Trim to get the actualy URL.
                        string url = urlPart.Substring(1, urlPart.Length - 2);
                        this.CurrentUrl = url;
                        this.HasNext = true;
                        break;
                    }
                }
            }

            return response;
        }

        public void UpdateAvailability(JObject response, int recordCount)
        {
            // Nothing to do. GitHub response headers already let us figure out whether we should continue batching or not.
        }
    }
}
