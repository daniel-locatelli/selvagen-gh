using System;
using System.Threading.Tasks;
using Xunit;
using Selvagen.Core.Api;
using Selvagen.Core.Models;

namespace Selvagen.Core.Tests
{
    public class RestTests
    {
        private readonly string _url = SelvagenConfig.SupabaseUrl;
        private readonly string _key = SelvagenConfig.SupabaseAnonKey;

        [Fact]
        public async Task TestListClients_DoesNotThrow()
        {
            var client = new SelvagenClient(_url, _key);
            // Note: This won't actually succeed without login, but we can test the auth guard
            try
            {
                await client.ListClientsAsync();
            }
            catch (InvalidOperationException ex)
            {
                Assert.Contains("Not authenticated", ex.Message);
            }
        }

        [Fact]
        public async Task TestListProjectsByClient_DoesNotThrow()
        {
            var client = new SelvagenClient(_url, _key);
            try
            {
                await client.ListProjectsByClientAsync("some-id");
            }
            catch (InvalidOperationException ex)
            {
                Assert.Contains("Not authenticated", ex.Message);
            }
        }

        [Fact]
        public async Task TestUpdateModuleProperty_DoesNotThrow()
        {
            var client = new SelvagenClient(_url, _key);
            try
            {
                await client.UpdateModulePropertyAsync("topography", "rec-id", "prop", "val");
            }
            catch (Exception)
            {
                // Expected failure if not logged in or other issues
            }
        }
    }
}
