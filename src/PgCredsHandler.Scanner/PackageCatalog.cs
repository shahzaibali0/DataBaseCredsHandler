using PgCredsHandler.Core.Models;

namespace PgCredsHandler.Scanner;

/// <summary>
/// The third-party packages worth showing on a project, by purpose. Anything not listed (build tools, type
/// packages, small utilities) is left out, so a project shows its handful of important choices, not 200 lines.
/// </summary>
public static class PackageCatalog
{
    // npm package (or scope prefix ending in "/") → category and the name people know it by.
    private static readonly Dictionary<string, (string Category, string Name)> Npm = new(StringComparer.OrdinalIgnoreCase)
    {
        ["prisma"] = ("Database", "Prisma"), ["@prisma/client"] = ("Database", "Prisma"),
        ["drizzle-orm"] = ("Database", "Drizzle"), ["typeorm"] = ("Database", "TypeORM"),
        ["sequelize"] = ("Database", "Sequelize"), ["mongoose"] = ("Database", "Mongoose"),
        ["pg"] = ("Database", "node-postgres"), ["knex"] = ("Database", "Knex"),
        ["@supabase/supabase-js"] = ("Database", "Supabase"), ["@neondatabase/serverless"] = ("Database", "Neon"),
        ["ioredis"] = ("Database", "Redis"), ["redis"] = ("Database", "Redis"),
        ["next-auth"] = ("Auth", "NextAuth"), ["@auth/core"] = ("Auth", "Auth.js"), ["@clerk/nextjs"] = ("Auth", "Clerk"),
        ["passport"] = ("Auth", "Passport"), ["jsonwebtoken"] = ("Auth", "JWT"), ["jose"] = ("Auth", "JOSE (JWT)"),
        ["bcrypt"] = ("Auth", "bcrypt"), ["bcryptjs"] = ("Auth", "bcrypt"), ["better-auth"] = ("Auth", "Better Auth"),
        ["stripe"] = ("Payments", "Stripe"), ["@stripe/stripe-js"] = ("Payments", "Stripe"),
        ["@paypal/react-paypal-js"] = ("Payments", "PayPal"), ["@lemonsqueezy/lemonsqueezy.js"] = ("Payments", "Lemon Squeezy"),
        ["@tanstack/react-query"] = ("Data fetching", "TanStack Query"), ["swr"] = ("Data fetching", "SWR"),
        ["axios"] = ("Data fetching", "Axios"), ["@trpc/server"] = ("Data fetching", "tRPC"),
        ["graphql"] = ("Data fetching", "GraphQL"), ["@apollo/client"] = ("Data fetching", "Apollo"),
        ["tailwindcss"] = ("UI", "Tailwind CSS"), ["@radix-ui/"] = ("UI", "Radix UI"), ["@base-ui-components/react"] = ("UI", "Base UI"),
        ["@mui/material"] = ("UI", "MUI"), ["antd"] = ("UI", "Ant Design"), ["@chakra-ui/react"] = ("UI", "Chakra UI"),
        ["framer-motion"] = ("UI", "Motion"), ["motion"] = ("UI", "Motion"), ["lucide-react"] = ("UI", "Lucide icons"),
        ["@angular/material"] = ("UI", "Angular Material"),
        ["zod"] = ("Validation", "Zod"), ["yup"] = ("Validation", "Yup"), ["react-hook-form"] = ("Validation", "React Hook Form"),
        ["zustand"] = ("State", "Zustand"), ["@reduxjs/toolkit"] = ("State", "Redux Toolkit"),
        ["nodemailer"] = ("Messaging", "Nodemailer"), ["resend"] = ("Messaging", "Resend"),
        ["@sendgrid/mail"] = ("Messaging", "SendGrid"), ["twilio"] = ("Messaging", "Twilio"),
        ["@aws-sdk/"] = ("Cloud & storage", "AWS SDK"), ["cloudinary"] = ("Cloud & storage", "Cloudinary"),
        ["firebase"] = ("Cloud & storage", "Firebase"), ["firebase-admin"] = ("Cloud & storage", "Firebase"),
        ["@vercel/blob"] = ("Cloud & storage", "Vercel Blob"), ["uploadthing"] = ("Cloud & storage", "UploadThing"),
        ["bullmq"] = ("Jobs", "BullMQ"), ["node-cron"] = ("Jobs", "node-cron"), ["inngest"] = ("Jobs", "Inngest"),
        ["@sentry/nextjs"] = ("Monitoring", "Sentry"), ["@sentry/node"] = ("Monitoring", "Sentry"),
        ["posthog-js"] = ("Monitoring", "PostHog"), ["posthog-node"] = ("Monitoring", "PostHog"),
        ["pino"] = ("Monitoring", "Pino"), ["winston"] = ("Monitoring", "Winston"),
        ["openai"] = ("AI", "OpenAI"), ["@anthropic-ai/sdk"] = ("AI", "Anthropic"), ["ai"] = ("AI", "Vercel AI SDK"),
        ["langchain"] = ("AI", "LangChain"),
        ["vitest"] = ("Testing", "Vitest"), ["jest"] = ("Testing", "Jest"), ["@playwright/test"] = ("Testing", "Playwright"),
        ["cypress"] = ("Testing", "Cypress"), ["@testing-library/react"] = ("Testing", "Testing Library"),
    };

    // NuGet package (or prefix ending in ".") → category and name.
    private static readonly Dictionary<string, (string Category, string Name)> NuGet = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Npgsql"] = ("Database", "Npgsql"), ["Npgsql.EntityFrameworkCore.PostgreSQL"] = ("Database", "EF Core (PostgreSQL)"),
        ["Microsoft.EntityFrameworkCore."] = ("Database", "EF Core"), ["Dapper"] = ("Database", "Dapper"),
        ["StackExchange.Redis"] = ("Database", "Redis"), ["MongoDB.Driver"] = ("Database", "MongoDB"),
        ["Microsoft.AspNetCore.Authentication.JwtBearer"] = ("Auth", "JWT bearer"),
        ["Microsoft.AspNetCore.Identity.EntityFrameworkCore"] = ("Auth", "ASP.NET Identity"),
        ["Stripe.net"] = ("Payments", "Stripe"),
        ["MediatR"] = ("Architecture", "MediatR"), ["AutoMapper"] = ("Architecture", "AutoMapper"),
        ["FluentValidation."] = ("Validation", "FluentValidation"), ["FluentValidation"] = ("Validation", "FluentValidation"),
        ["Swashbuckle.AspNetCore"] = ("API docs", "Swagger"), ["Scalar.AspNetCore"] = ("API docs", "Scalar"),
        ["Serilog."] = ("Monitoring", "Serilog"), ["Serilog"] = ("Monitoring", "Serilog"),
        ["Hangfire."] = ("Jobs", "Hangfire"), ["Quartz"] = ("Jobs", "Quartz.NET"), ["MassTransit"] = ("Messaging", "MassTransit"),
        ["AWSSDK."] = ("Cloud & storage", "AWS SDK"), ["Azure.Storage.Blobs"] = ("Cloud & storage", "Azure Blob Storage"),
        ["xunit"] = ("Testing", "xUnit"), ["NUnit"] = ("Testing", "NUnit"), ["Moq"] = ("Testing", "Moq"),
    };

    public static IReadOnlyList<KeyPackage> ForNpm(IEnumerable<string> packageNames) => Lookup(packageNames, Npm, "/");

    public static IReadOnlyList<KeyPackage> ForNuGet(IEnumerable<string> packageNames) => Lookup(packageNames, NuGet, ".");

    private static IReadOnlyList<KeyPackage> Lookup(
        IEnumerable<string> packageNames, Dictionary<string, (string Category, string Name)> catalog, string prefixEnd) =>
        packageNames
            .Select(name => Find(name, catalog, prefixEnd))
            .OfType<KeyPackage>()
            .Distinct()
            .OrderBy(package => package.Category, StringComparer.Ordinal)
            .ThenBy(package => package.Name, StringComparer.Ordinal)
            .ToList();

    private static KeyPackage? Find(string packageName, Dictionary<string, (string Category, string Name)> catalog, string prefixEnd)
    {
        if (catalog.TryGetValue(packageName, out var exact)) return new KeyPackage(exact.Category, exact.Name);

        var prefix = catalog.Keys.FirstOrDefault(key =>
            key.EndsWith(prefixEnd, StringComparison.Ordinal) && packageName.StartsWith(key, StringComparison.OrdinalIgnoreCase));
        return prefix is null ? null : new KeyPackage(catalog[prefix].Category, catalog[prefix].Name);
    }
}
