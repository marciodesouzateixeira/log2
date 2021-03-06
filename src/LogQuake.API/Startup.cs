﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using LogQuake.Domain.Context;
using LogQuake.Domain.Interfaces;
using LogQuake.Domain.Interfaces.Repositories;
using LogQuake.Infra.Data.Contexto;
using LogQuake.Infra.Data.Repositories;
using LogQuake.Infra.Data.SqlServerContext;
using LogQuake.Infra.UoW;
using LogQuake.Service.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Serialization;
using Swashbuckle.AspNetCore.Swagger;

namespace LogQuake.API
{
    public class Startup
    {
        public IConfiguration Configuration { get; set; }
        private readonly ILogger _logger;

        public Startup(IConfiguration configuration, ILogger<Startup> logger)
        {
            Configuration = configuration;
            _logger = logger;

            string BancoDeDados = Configuration["DataBase"].ToString().ToUpper();
            if (BancoDeDados == "SQLITE")
            {
                string stringConnection = this.Configuration.GetConnectionString("SQLiteConnectionString");
                DbContextOptionsBuilder optionsBuilder = new DbContextOptionsBuilder<LogQuakeContext>();
                optionsBuilder.UseSqlite(stringConnection);

                // Cria o Banco de Dados para SqLite
                using (var client = new SQLiteLogQuakeContext(optionsBuilder.Options))
                {
                    client.Database.EnsureCreated();
                }
            }
        }
                
        // This method gets called by the runtime. Use this method to add services to the container..
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<IFileProvider>(
                new PhysicalFileProvider(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"))
            );

            //Adicionando recurso de Cache em Controllers
            services.AddResponseCaching();

            services.AddMvc((options) => {
                //criando perfil de Cache nas controllers
                options.CacheProfiles.Add("default", new CacheProfile()
                {
                    Duration = 30//, tempo definido em segundos
                    //Location = ResponseCacheLocation.None
                });
                options.CacheProfiles.Add("MyCache", new CacheProfile()
                {
                    Duration = 60,
                    Location = ResponseCacheLocation.Any
                });
                // Definindo como PascalCase o retorno JSON do métodos das Controllers
            }).AddJsonOptions(options => options.SerializerSettings.ContractResolver = new DefaultContractResolver());


            services.AddSingleton<IConfiguration>(Configuration);

            services.AddMemoryCache();

            //Incluindo compressão nos retornos dos métodos as Controllers
            services.Configure<GzipCompressionProviderOptions>(options => options.Level = CompressionLevel.Optimal);

            services.AddResponseCompression(options =>
            {
                options.Providers.Add<GzipCompressionProvider>();
            });


            //definindo URL do servidor de Identity
            var authUrl = "http://localhost:59329/";

            services.AddAuthentication("Bearer")
                .AddIdentityServerAuthentication(options =>
                {
                    options.Authority = authUrl;
                    options.RequireHttpsMetadata = false;
                    options.ApiName = "LogQuake";
                   
                });

            //Criando Perfis de Autorização
            //Exemplo: Perfil Admin -> necessita da regra admin
            //Exemplo: Perfil Consulta -> necessita da regra consulta
            services.AddAuthorization(options =>
            {
                options.AddPolicy("Admin", policy =>
                {
                    policy.RequireClaim("role", "admin");
                });
                options.AddPolicy("Consulta", policy =>
                {
                    policy.RequireClaim("role", "consulta");
                });
            }
            );

            // Register the Swagger generator, defining 1 or more Swagger documents
            services.AddSwaggerGen(c =>
            {
                c.OperationFilter<FileOperationFilter>();
                c.SwaggerDoc("v1", new Info
                {
                    Version = "v1",
                    Title = "Log - Quake III Arena",
                    Description = "API para retornar informações das partidas realizadas no jogo Quake III Arena como por exemplo, quantidades de participantes, total de mortes e quantidades de mortes por jogador. " + Environment.NewLine + "Essas informações são obtidas através do log gerado pelo próprio jogo.",
                    TermsOfService = "None",
                    Contact = new Contact
                    {
                        Name = "Márcio de Souza Teixeira",
                        Email = "marcio79.teixeira@gmail.com",
                        Url = ""
                    },
                    License = new License
                    {
                        Name = "Use under LICX",
                        Url = "https://example.com/license"
                    }
                });

                c.AddSecurityDefinition("Bearer", new ApiKeyScheme
                {
                    Description = "Authorization header usando o tipo Bearer, por exemplo: \"Authorization: Bearer {token}\"",
                    Name = "Authorization",
                    In = "header",
                    Type = "apiKey"
                });

                c.AddSecurityRequirement(new Dictionary<string, IEnumerable<string>>
                {
                    { "Bearer", new string[] { } }
                });

                // Set the comments path for the Swagger JSON and UI.
                var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                c.IncludeXmlComments(xmlPath);
            });

            string BancoDeDados = Configuration["DataBase"].ToString().ToUpper();
            if (BancoDeDados == "SQLSERVER")
            {
                services.AddDbContext<LogQuakeContext, SqlServerLogQuakeContext>(options =>
                    options.UseSqlServer(Configuration.GetConnectionString("SqlServerConnectionString"), b => b.UseRowNumberForPaging()));
            }
            else
            {
                services.AddDbContext<LogQuakeContext, SQLiteLogQuakeContext>(options =>
                    options.UseSqlite(Configuration.GetConnectionString("SQLiteConnectionString")));
            }


            services.AddScoped<ILogQuakeService, LogQuakeService>();
            services.AddScoped<IKillRepository, KillRepository>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();

            _logger.LogInformation("Adicionado KillRepository ao services");
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            //Adicionando compressão nos retornos dos métodos das Controllers.
            //Para a compressão funcionar a linha baixo deve vir antes da app.UseStaticFiles();
            app.UseResponseCompression();

            app.UseStaticFiles();

            //Adicionando recurso de Autenticação em Controllers, esse recurso é utilizado em conjunto com IdentityServer
            app.UseAuthentication();

            //Adicionando recurso de Cache em Controllers
            app.UseResponseCaching();

            app.UseMvc();


            // Enable middleware to serve generated Swagger as a JSON endpoint.
            app.UseSwagger();

            // Enable middleware to serve swagger-ui (HTML, JS, CSS, etc.), 
            // specifying the Swagger JSON endpoint.
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Log - Quake III Arena");
                c.RoutePrefix = string.Empty;
            });

        }

    }
}
