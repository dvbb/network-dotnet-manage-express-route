// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager.Samples.Common;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager;
using Azure.ResourceManager.Network;
using Azure.ResourceManager.Network.Models;

namespace ManageExpressRoute
{
    public class Program
    {
        private static ResourceIdentifier? _resourceGroupId = null;

        /**
        * Azure Network sample for managing express route circuits.
         *  - Create Express Route circuit
         *  - Create Express Route circuit peering. Please note: express route circuit should be provisioned by connectivity provider before this step.
         *  - Adding authorization to express route circuit
         *  - Create virtual network to be associated with virtual network gateway
         *  - Create virtual network gateway
         *  - Create virtual network gateway connection
        */
        public static async Task RunSample(ArmClient client)
        {
            string rgName = Utilities.CreateRandomName("NetworkSampleRG");
            string ercName = Utilities.CreateRandomName("erc");
            string vnetName = Utilities.CreateRandomName("vnet");
            string pipName = Utilities.CreateRandomName("pip");
            string gatewayName = Utilities.CreateRandomName("gateway");
            string connectionName = Utilities.CreateRandomName("con");

            {
                // Get default subscription
                SubscriptionResource subscription = await client.GetDefaultSubscriptionAsync();

                // Create a resource group in the EastUS region
                Utilities.Log($"Creating resource group...");
                ArmOperation<ResourceGroupResource> rgLro = await subscription.GetResourceGroups().CreateOrUpdateAsync(WaitUntil.Completed, rgName, new ResourceGroupData(AzureLocation.EastUS));
                ResourceGroupResource resourceGroup = rgLro.Value;
                _resourceGroupId = resourceGroup.Id;
                Utilities.Log("Created a resource group with name: " + resourceGroup.Data.Name);

                //============================================================
                // create Express Route Circuit
                Utilities.Log("Creating express route circuit...");
                //IExpressRouteCircuit erc = azure.ExpressRouteCircuits.Define(ercName)
                //    .WithRegion(region)
                //    .WithNewResourceGroup(rgName)
                //    .WithServiceProvider("Equinix")
                //    .WithPeeringLocation("Silicon Valley")
                //    .WithBandwidthInMbps(50)
                //    .WithSku(ExpressRouteCircuitSkuType.PremiumMeteredData)
                //    .Create();
                ExpressRouteCircuitData circuitInput = new ExpressRouteCircuitData()
                {
                    Location = resourceGroup.Data.Location,
                    Tags = { { "key", "value" } },
                    Sku = new ExpressRouteCircuitSku
                    {
                        Name = "Premium_MeteredData",
                        Tier = "Premium",
                        Family = "MeteredData"
                    },
                    ServiceProviderProperties = new ExpressRouteCircuitServiceProviderProperties
                    {
                        BandwidthInMbps = Convert.ToInt32(200),
                        PeeringLocation = "boydton 1 dc",
                        ServiceProviderName = "bvtazureixp01"
                    }
                };
                var ercLro = await resourceGroup.GetExpressRouteCircuits().CreateOrUpdateAsync(WaitUntil.Completed, ercName, circuitInput);
                ExpressRouteCircuitResource erc = ercLro.Value;
                Utilities.Log($"Created express route circuit: {erc.Data.Name}");

                //============================================================
                // Create Express Route circuit peering. Please note: express route circuit should be provisioned by connectivity provider before this step.
                Utilities.Log("Creating express route circuit peering...");
                //erc.Peerings.DefineAzurePrivatePeering()
                //    .WithPrimaryPeerAddressPrefix("123.0.0.0/30")
                //    .WithSecondaryPeerAddressPrefix("123.0.0.4/30")
                //    .WithVlanId(200)
                //    .WithPeerAsn(100)
                //    .Create();
                var peering = new ExpressRouteCircuitPeeringData()
                {
                    Name = ExpressRoutePeeringType.MicrosoftPeering.ToString(),
                    PeeringType = ExpressRoutePeeringType.MicrosoftPeering,
                    PeerASN = Convert.ToInt32(1000),
                    VlanId = Convert.ToInt32(400),
                    PrimaryPeerAddressPrefix = "199.168.200.0/30",
                    SecondaryPeerAddressPrefix = "199.168.202.0/30",
                    MicrosoftPeeringConfig = new ExpressRouteCircuitPeeringConfig()
                    {
                        AdvertisedPublicPrefixes = { "fc02::1/128" },
                        LegacyMode = Convert.ToInt32(true)
                    },
                };
                resourceGroup.GetExpressRouteCircuits();
                var ercPeeringLro = await erc.GetExpressRouteCircuitPeerings().CreateOrUpdateAsync(WaitUntil.Completed, "MicrosoftPeering", peering);
                ExpressRouteCircuitPeeringResource ercPeering = ercPeeringLro.Value;
                Utilities.Log($"Created express route circuit peering: {ercPeering.Data.Name}");

                //============================================================
                // Adding authorization to express route circuit
                //erc.Update()
                //    .WithAuthorization("myAuthorization")
                //    .Apply();

                //============================================================
                // Create virtual network to be associated with virtual network gateway
                Utilities.Log("Creating virtual network...");
                VirtualNetworkData vnetInput = new VirtualNetworkData()
                {
                    Location = resourceGroup.Data.Location,
                    AddressPrefixes = { "192.168.0.0/16" },
                    Subnets =
                    {
                        new SubnetData() { AddressPrefix = "192.168.200.0/26", Name = "GatewaySubnet" },
                        new SubnetData() { AddressPrefix = "192.168.1.0/24", Name = "FrontEnd" }
                    },
                };
                var vnetLro = await resourceGroup.GetVirtualNetworks().CreateOrUpdateAsync(WaitUntil.Completed, vnetName, vnetInput);
                VirtualNetworkResource vnet = vnetLro.Value;
                Utilities.Log($"Created a virtual network: {vnet.Data.Name}");

                //============================================================
                // Create public ip for virtual network gateway
                var pip = await Utilities.CreatePublicIP(resourceGroup, pipName);

                // Create virtual network gateway
                Utilities.Log("Creating virtual network gateway...");
                //IVirtualNetworkGateway vngw1 = azure.VirtualNetworkGateways.Define(gatewayName)
                //    .WithRegion(region)
                //    .WithNewResourceGroup(rgName)
                //    .WithExistingNetwork(network)
                //    .WithExpressRoute()
                //    .WithSku(VirtualNetworkGatewaySkuName.Standard)
                //    .Create();
                VirtualNetworkGatewayData vpnGatewayInput = new VirtualNetworkGatewayData()
                {
                    Location = resourceGroup.Data.Location,
                    Sku = new VirtualNetworkGatewaySku()
                    {
                        Name = VirtualNetworkGatewaySkuName.Basic,
                        Tier = VirtualNetworkGatewaySkuTier.Basic
                    },
                    Tags = { { "key", "value" } },
                    EnableBgp = false,
                    GatewayType = VirtualNetworkGatewayType.Vpn,
                    VpnType = VpnType.RouteBased,
                    IPConfigurations =
                    {
                        new VirtualNetworkGatewayIPConfiguration()
                        {
                            Name = Utilities.CreateRandomName("config"),
                            PrivateIPAllocationMethod = NetworkIPAllocationMethod.Dynamic,
                            PublicIPAddressId  = pip.Data.Id,
                            SubnetId = vnet.Data.Subnets.First(item => item.Name == "GatewaySubnet").Id,
                        }
                    },
                };
                var vpnGatewayLro = await resourceGroup.GetVirtualNetworkGateways().CreateOrUpdateAsync(WaitUntil.Completed, gatewayName, vpnGatewayInput);
                VirtualNetworkGatewayResource vpnGateway = vpnGatewayLro.Value;
                Utilities.Log($"Created virtual network gateway: {vpnGateway.Data.Name}");

                //============================================================
                // Create virtual network gateway connection
                //Utilities.Log("Creating virtual network gateway connection...");
                //vngw1.Connections.Define(connectionName)
                //    .WithExpressRoute(erc)
                //    // Note: authorization key is required only in case express route circuit and virtual network gateway are in different subscriptions
                //    // .WithAuthorization(erc.Inner.Authorizations.First().AuthorizationKey)
                //    .Create();
                //Utilities.Log("Created virtual network gateway connection");
            }
            {
                try
                {
                    if (_resourceGroupId is not null)
                    {
                        Utilities.Log($"Deleting Resource Group...");
                        await client.GetResourceGroupResource(_resourceGroupId).DeleteAsync(WaitUntil.Completed);
                        Utilities.Log($"Deleted Resource Group: {_resourceGroupId.Name}");
                    }
                }
                catch (NullReferenceException)
                {
                    Utilities.Log("Did not create any resources in Azure. No clean up is necessary");
                }
                catch (Exception ex)
                {
                    Utilities.Log(ex);
                }
            }
        }

        public static async Task Main(string[] args)
        {
            //=================================================================
            // Authenticate
            var clientId = Environment.GetEnvironmentVariable("CLIENT_ID");
            var clientSecret = Environment.GetEnvironmentVariable("CLIENT_SECRET");
            var tenantId = Environment.GetEnvironmentVariable("TENANT_ID");
            var subscription = Environment.GetEnvironmentVariable("SUBSCRIPTION_ID");
            ClientSecretCredential credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
            ArmClient client = new ArmClient(credential, subscription);

            await RunSample(client);
            try
            {
                //=================================================================
                // Authenticate

            }
            catch (Exception ex)
            {
                Utilities.Log(ex);
            }
        }
    }
}
