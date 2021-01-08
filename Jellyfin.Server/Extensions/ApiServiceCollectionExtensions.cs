using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using Emby.Server.Implementations;
using Jellyfin.Api.Auth;
using Jellyfin.Api.Auth.DefaultAuthorizationPolicy;
using Jellyfin.Api.Auth.DownloadPolicy;
using Jellyfin.Api.Auth.FirstTimeOrIgnoreParentalControlSetupPolicy;
using Jellyfin.Api.Auth.FirstTimeSetupOrDefaultPolicy;
using Jellyfin.Api.Auth.FirstTimeSetupOrElevatedPolicy;
using Jellyfin.Api.Auth.IgnoreParentalControlPolicy;
using Jellyfin.Api.Auth.LocalAccessOrRequiresElevationPolicy;
using Jellyfin.Api.Auth.LocalAccessPolicy;
using Jellyfin.Api.Auth.RequiresElevationPolicy;
using Jellyfin.Api.Auth.SyncPlayAccessPolicy;
using Jellyfin.Api.Constants;
using Jellyfin.Api.Controllers;
using Jellyfin.Api.ModelBinders;
using Jellyfin.Data.Enums;
using Jellyfin.Networking.Configuration;
using Jellyfin.Server.Configuration;
using Jellyfin.Server.Filters;
using Jellyfin.Server.Formatters;
using MediaBrowser.Common.Json;
using MediaBrowser.Common.Net;
using MediaBrowser.Model.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Interfaces;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using AuthenticationSchemes = Jellyfin.Api.Constants.AuthenticationSchemes;

namespace Jellyfin.Server.Extensions
{
    /// <summary>
    /// API specific extensions for the service collection.
    /// </summary>
    public static class ApiServiceCollectionExtensions
    {
        /// <summary>
        /// Adds jellyfin API authorization policies to the DI container.
        /// </summary>
        /// <param name="serviceCollection">The service collection.</param>
        /// <returns>The updated service collection.</returns>
        public static IServiceCollection AddJellyfinApiAuthorization(this IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton<IAuthorizationHandler, DefaultAuthorizationHandler>();
            serviceCollection.AddSingleton<IAuthorizationHandler, DownloadHandler>();
            serviceCollection.AddSingleton<IAuthorizationHandler, FirstTimeSetupOrDefaultHandler>();
            serviceCollection.AddSingleton<IAuthorizationHandler, FirstTimeSetupOrElevatedHandler>();
            serviceCollection.AddSingleton<IAuthorizationHandler, IgnoreParentalControlHandler>();
            serviceCollection.AddSingleton<IAuthorizationHandler, FirstTimeOrIgnoreParentalControlSetupHandler>();
            serviceCollection.AddSingleton<IAuthorizationHandler, LocalAccessHandler>();
            serviceCollection.AddSingleton<IAuthorizationHandler, LocalAccessOrRequiresElevationHandler>();
            serviceCollection.AddSingleton<IAuthorizationHandler, RequiresElevationHandler>();
            serviceCollection.AddSingleton<IAuthorizationHandler, SyncPlayAccessHandler>();
            return serviceCollection.AddAuthorizationCore(options =>
            {
                options.AddPolicy(
                    Policies.DefaultAuthorization,
                    policy =>
                    {
                        policy.AddAuthenticationSchemes(AuthenticationSchemes.CustomAuthentication);
                        policy.AddRequirements(new DefaultAuthorizationRequirement());
                    });
                options.AddPolicy(
                    Policies.Download,
                    policy =>
                    {
                        policy.AddAuthenticationSchemes(AuthenticationSchemes.CustomAuthentication);
                        policy.AddRequirements(new DownloadRequirement());
                    });
                options.AddPolicy(
                    Policies.FirstTimeSetupOrDefault,
                    policy =>
                    {
                        policy.AddAuthenticationSchemes(AuthenticationSchemes.CustomAuthentication);
                        policy.AddRequirements(new FirstTimeSetupOrDefaultRequirement());
                    });
                options.AddPolicy(
                    Policies.FirstTimeSetupOrElevated,
                    policy =>
                    {
                        policy.AddAuthenticationSchemes(AuthenticationSchemes.CustomAuthentication);
                        policy.AddRequirements(new FirstTimeSetupOrElevatedRequirement());
                    });
                options.AddPolicy(
                    Policies.IgnoreParentalControl,
                    policy =>
                    {
                        policy.AddAuthenticationSchemes(AuthenticationSchemes.CustomAuthentication);
                        policy.AddRequirements(new IgnoreParentalControlRequirement());
                    });
                options.AddPolicy(
                    Policies.FirstTimeSetupOrIgnoreParentalControl,
                    policy =>
                    {
                        policy.AddAuthenticationSchemes(AuthenticationSchemes.CustomAuthentication);
                        policy.AddRequirements(new FirstTimeOrIgnoreParentalControlSetupRequirement());
                    });
                options.AddPolicy(
                    Policies.LocalAccessOnly,
                    policy =>
                    {
                        policy.AddAuthenticationSchemes(AuthenticationSchemes.CustomAuthentication);
                        policy.AddRequirements(new LocalAccessRequirement());
                    });
                options.AddPolicy(
                    Policies.LocalAccessOrRequiresElevation,
                    policy =>
                    {
                        policy.AddAuthenticationSchemes(AuthenticationSchemes.CustomAuthentication);
                        policy.AddRequirements(new LocalAccessOrRequiresElevationRequirement());
                    });
                options.AddPolicy(
                    Policies.RequiresElevation,
                    policy =>
                    {
                        policy.AddAuthenticationSchemes(AuthenticationSchemes.CustomAuthentication);
                        policy.AddRequirements(new RequiresElevationRequirement());
                    });
                options.AddPolicy(
                    Policies.SyncPlayHasAccess,
                    policy =>
                    {
                        policy.AddAuthenticationSchemes(AuthenticationSchemes.CustomAuthentication);
                        policy.AddRequirements(new SyncPlayAccessRequirement(SyncPlayAccessRequirementType.HasAccess));
                    });
                options.AddPolicy(
                    Policies.SyncPlayCreateGroup,
                    policy =>
                    {
                        policy.AddAuthenticationSchemes(AuthenticationSchemes.CustomAuthentication);
                        policy.AddRequirements(new SyncPlayAccessRequirement(SyncPlayAccessRequirementType.CreateGroup));
                    });
                options.AddPolicy(
                    Policies.SyncPlayJoinGroup,
                    policy =>
                    {
                        policy.AddAuthenticationSchemes(AuthenticationSchemes.CustomAuthentication);
                        policy.AddRequirements(new SyncPlayAccessRequirement(SyncPlayAccessRequirementType.JoinGroup));
                    });
                options.AddPolicy(
                    Policies.SyncPlayIsInGroup,
                    policy =>
                    {
                        policy.AddAuthenticationSchemes(AuthenticationSchemes.CustomAuthentication);
                        policy.AddRequirements(new SyncPlayAccessRequirement(SyncPlayAccessRequirementType.IsInGroup));
                    });
            });
        }

        /// <summary>
        /// Adds custom legacy authentication to the service collection.
        /// </summary>
        /// <param name="serviceCollection">The service collection.</param>
        /// <returns>The updated service collection.</returns>
        public static AuthenticationBuilder AddCustomAuthentication(this IServiceCollection serviceCollection)
        {
            return serviceCollection.AddAuthentication(AuthenticationSchemes.CustomAuthentication)
                .AddScheme<AuthenticationSchemeOptions, CustomAuthenticationHandler>(AuthenticationSchemes.CustomAuthentication, null);
        }

        /// <summary>
        /// Sets up the proxy configuration based on the addresses in <paramref name="userList"/>.
        /// </summary>
        /// <param name="networkManager">The <see cref="INetworkManager"/> instance.</param>
        /// <param name="config">The <see cref="NetworkConfiguration"/> containing the config settings.</param>
        /// <param name="userList">The string array to parse.</param>
        /// <param name="options">The <see cref="ForwardedHeadersOptions"/> instance.</param>
        public static void ParseList(INetworkManager networkManager, NetworkConfiguration config, string[] userList, ForwardedHeadersOptions options)
        {
            for (var i = 0; i < userList.Length; i++)
            {
                if (IPNetAddress.TryParse(userList[i], out var addr))
                {
                    if ((!config.EnableIPV4 && addr.AddressFamily == AddressFamily.InterNetwork)
                         || (!config.EnableIPV6 && addr.AddressFamily == AddressFamily.InterNetworkV6))
                    {
                        continue;
                    }

                    if (networkManager.SystemIP6Enabled && addr.AddressFamily == AddressFamily.InterNetwork)
                    {
                        // If the server is using dual-mode sockets, IPv4 addresses are supplied in an IPv6 format.
                        // https://docs.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-5.0 .
                        addr.Address = addr.Address.MapToIPv6();
                    }

                    if (addr.PrefixLength == 32)
                    {
                        options.KnownProxies.Add(addr.Address);
                    }
                    else
                    {
                        options.KnownNetworks.Add(new IPNetwork(addr.Address, addr.PrefixLength));
                    }
                }
                else if (IPHost.TryParse(userList[i], out var host))
                {
                    foreach (var address in host.GetAddresses())
                    {
                        if ((!config.EnableIPV4 && host.AddressFamily == AddressFamily.InterNetwork)
                            || (!config.EnableIPV6 && host.AddressFamily == AddressFamily.InterNetworkV6))
                        {
                            continue;
                        }

                        var hostAddr = address;
                        if (networkManager.SystemIP6Enabled && address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            // If the server is using dual-mode sockets, IPv4 addresses are supplied in an IPv6 format.
                            // https://docs.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-5.0 .
                            hostAddr = address.MapToIPv6();
                        }

                        options.KnownProxies.Add(hostAddr);
                    }
                }
            }
        }

        private static string EnumToString<T>(IEnumerable<T> x)
        {
            var sb = new StringBuilder();
            foreach (var item in x)
            {
                if (item is IPAddress ipItem)
                {
                    sb.Append(ipItem.ToString());
                }
                else if (item is IPNetwork ipNetwork)
                {
                    sb.Append(ipNetwork.Prefix.ToString());
                    sb.Append('/');
                    sb.Append(ipNetwork.PrefixLength.ToString(CultureInfo.InvariantCulture));
                    sb.Append(',');
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Extension method for adding the jellyfin API to the service collection.
        /// </summary>
        /// <param name="serviceCollection">The service collection.</param>
        /// <param name="pluginAssemblies">An IEnumerable containing all plugin assemblies with API controllers.</param>
        /// <param name="config">The <see cref="NetworkConfiguration"/>.</param>
        /// <param name="networkManager">The <see cref="INetworkManager"/> instance.</param>
        /// <returns>The MVC builder.</returns>
        public static IMvcBuilder AddJellyfinApi(this IServiceCollection serviceCollection, IEnumerable<Assembly> pluginAssemblies, NetworkConfiguration config, INetworkManager networkManager)
        {
            IMvcBuilder mvcBuilder = serviceCollection
            .AddCors()
            .AddTransient<ICorsPolicyProvider, CorsPolicyProvider>()
            .Configure<ForwardedHeadersOptions>(options =>
                {
                    // https://github.com/dotnet/aspnetcore/blob/master/src/Middleware/HttpOverrides/src/ForwardedHeadersMiddleware.cs
                    // Enable debug logging on Microsoft.AspNetCore.HttpOverrides.ForwardedHeadersMiddleware to help investigate issues.

                    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                    if (config.KnownProxies.Length == 0)
                    {
                        options.KnownProxies.Clear();
                        options.KnownNetworks.Clear();
                    }
                    else
                    {
                        ParseList(networkManager, config, config.KnownProxies, options);
                        networkManager.Log("KnownProxies: " + EnumToString<IPAddress>(options.KnownProxies));
                        networkManager.Log("KnownNetworks: " + EnumToString<IPNetwork>(options.KnownNetworks));
                    }

                    // Only set forward limit if we have some known proxies or some known networks.
                    if (options.KnownProxies.Count != 0 || options.KnownNetworks.Count != 0)
                    {
                        options.ForwardLimit = null;
                    }

                    networkManager.Log("Forward Limit : " + options.ForwardLimit?.ToString(CultureInfo.CurrentCulture) ?? "None");
                })
                .AddMvc(opts =>
                {
                    // Allow requester to change between camelCase and PascalCase
                    opts.RespectBrowserAcceptHeader = true;

                    opts.OutputFormatters.Insert(0, new CamelCaseJsonProfileFormatter());
                    opts.OutputFormatters.Insert(0, new PascalCaseJsonProfileFormatter());

                    opts.OutputFormatters.Add(new CssOutputFormatter());
                    opts.OutputFormatters.Add(new XmlOutputFormatter());

                    opts.ModelBinderProviders.Insert(0, new NullableEnumModelBinderProvider());
                })

                // Clear app parts to avoid other assemblies being picked up
                .ConfigureApplicationPartManager(a => a.ApplicationParts.Clear())
                .AddApplicationPart(typeof(StartupController).Assembly)
                .AddJsonOptions(options =>
                {
                    // Update all properties that are set in JsonDefaults
                    var jsonOptions = JsonDefaults.GetPascalCaseOptions();

                    // From JsonDefaults
                    options.JsonSerializerOptions.ReadCommentHandling = jsonOptions.ReadCommentHandling;
                    options.JsonSerializerOptions.WriteIndented = jsonOptions.WriteIndented;
                    options.JsonSerializerOptions.DefaultIgnoreCondition = jsonOptions.DefaultIgnoreCondition;
                    options.JsonSerializerOptions.NumberHandling = jsonOptions.NumberHandling;
                    options.JsonSerializerOptions.PropertyNameCaseInsensitive = jsonOptions.PropertyNameCaseInsensitive;

                    options.JsonSerializerOptions.Converters.Clear();
                    foreach (var converter in jsonOptions.Converters)
                    {
                        options.JsonSerializerOptions.Converters.Add(converter);
                    }

                    // From JsonDefaults.PascalCase
                    options.JsonSerializerOptions.PropertyNamingPolicy = jsonOptions.PropertyNamingPolicy;
                });

            foreach (Assembly pluginAssembly in pluginAssemblies)
            {
                mvcBuilder.AddApplicationPart(pluginAssembly);
            }

            return mvcBuilder.AddControllersAsServices();
        }

        /// <summary>
        /// Adds Swagger to the service collection.
        /// </summary>
        /// <param name="serviceCollection">The service collection.</param>
        /// <returns>The updated service collection.</returns>
        public static IServiceCollection AddJellyfinApiSwagger(this IServiceCollection serviceCollection)
        {
            return serviceCollection.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("api-docs", new OpenApiInfo
                {
                    Title = "Jellyfin API",
                    Version = "v1",
                    Extensions = new Dictionary<string, IOpenApiExtension>
                    {
                        {
                            "x-jellyfin-version",
                            new OpenApiString(typeof(ApplicationHost).Assembly.GetName().Version?.ToString())
                        }
                    }
                });

                c.AddSecurityDefinition(AuthenticationSchemes.CustomAuthentication, new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.ApiKey,
                    In = ParameterLocation.Header,
                    Name = "X-Emby-Authorization",
                    Description = "API key header parameter"
                });

                // Add all xml doc files to swagger generator.
                var xmlFiles = Directory.GetFiles(
                    AppContext.BaseDirectory,
                    "*.xml",
                    SearchOption.TopDirectoryOnly);

                foreach (var xmlFile in xmlFiles)
                {
                    c.IncludeXmlComments(xmlFile);
                }

                // Order actions by route path, then by http method.
                c.OrderActionsBy(description =>
                    $"{description.ActionDescriptor.RouteValues["controller"]}_{description.RelativePath}");

                // Use method name as operationId
                c.CustomOperationIds(
                    description =>
                    {
                        description.TryGetMethodInfo(out MethodInfo methodInfo);
                        // Attribute name, method name, none.
                        return description?.ActionDescriptor?.AttributeRouteInfo?.Name
                               ?? methodInfo?.Name
                               ?? null;
                    });

                // TODO - remove when all types are supported in System.Text.Json
                c.AddSwaggerTypeMappings();

                c.OperationFilter<SecurityRequirementsOperationFilter>();
                c.OperationFilter<FileResponseFilter>();
                c.DocumentFilter<WebsocketModelFilter>();
            });
        }

        private static void AddSwaggerTypeMappings(this SwaggerGenOptions options)
        {
            /*
             * TODO remove when System.Text.Json properly supports non-string keys.
             * Used in BaseItemDto.ImageBlurHashes
             */
            options.MapType<Dictionary<ImageType, string>>(() =>
                new OpenApiSchema
                {
                    Type = "object",
                    AdditionalProperties = new OpenApiSchema
                    {
                        Type = "string"
                    }
                });

            /*
             * Support BlurHash dictionary
             */
            options.MapType<Dictionary<ImageType, Dictionary<string, string>>>(() =>
                new OpenApiSchema
                {
                    Type = "object",
                    Properties = typeof(ImageType).GetEnumNames().ToDictionary(
                        name => name,
                        name => new OpenApiSchema
                        {
                            Type = "object",
                            AdditionalProperties = new OpenApiSchema
                            {
                                Type = "string"
                            }
                        })
                });
        }
    }
}
