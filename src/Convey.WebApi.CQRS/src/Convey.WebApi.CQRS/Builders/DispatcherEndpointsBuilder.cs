using System;
using System.Net;
using System.Threading.Tasks;
using Convey.CQRS.Commands;
using Convey.CQRS.Queries;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Convey.WebApi.CQRS.Builders;

public class DispatcherEndpointsBuilder : IDispatcherEndpointsBuilder
{
    private readonly IEndpointsBuilder _builder;

    public DispatcherEndpointsBuilder(IEndpointsBuilder builder)
    {
        _builder = builder;
    }

    public IDispatcherEndpointsBuilder Get(string path, Func<HttpContext, Task> context = null,
        Action<IEndpointConventionBuilder> endpoint = null, bool auth = false, string roles = null,
        params string[] policies)
    {
        _builder.Get(path, context, endpoint, auth, roles, policies);

        return this;
    }

    public IDispatcherEndpointsBuilder Get<TQuery, TResult>(string path,
        Func<TQuery, HttpContext, Task> beforeDispatch = null,
        Func<TQuery, TResult, HttpContext, Task> afterDispatch = null,
        Action<IEndpointConventionBuilder> endpoint = null, bool auth = false, string roles = null,
        params string[] policies) where TQuery : class, IQuery<TResult>
    {
        _builder.Get<TQuery, TResult>(path, async (query, ctx) =>
        {
            if (beforeDispatch is not null)
            {
                await beforeDispatch.Invoke(query, ctx);
            }

            var dispatcher = ctx.RequestServices.GetRequiredService<IQueryDispatcher>();
            var result = await dispatcher.QueryAsync<TQuery, TResult>(query);
            if (afterDispatch is null)
            {
                if (result is null)
                {
                    ctx.Response.StatusCode = (int)HttpStatusCode.NoContent;
                    return;
                }

                await ctx.Response.WriteJsonAsync(result);
                return;
            }

            await afterDispatch.Invoke(query, result, ctx);
        }, endpoint, auth, roles, policies);

        return this;
    }

    public IDispatcherEndpointsBuilder Head(string path, Func<HttpContext, Task> context = null,
        Action<IEndpointConventionBuilder> endpoint = null, bool auth = false, string roles = null,
        params string[] policies)
    {
        _builder.Head(path, context, endpoint, auth, roles, policies);

        return this;
    }

    public IDispatcherEndpointsBuilder Head<TQuery>(string path,
        Func<TQuery, HttpContext, Task> beforeDispatch = null,
        Func<TQuery, bool, HttpContext, Task> afterDispatch = null,
        Action<IEndpointConventionBuilder> endpoint = null, bool auth = false, string roles = null,
        params string[] policies) where TQuery : class, IQuery<bool>
    {
        _builder.Head<TQuery>(path, async (query, ctx) =>
        {
            if (beforeDispatch is not null)
            {
                await beforeDispatch.Invoke(query, ctx);
            }

            var dispatcher = ctx.RequestServices.GetRequiredService<IQueryDispatcher>();
            var result = await dispatcher.QueryAsync<TQuery, bool>(query);
            if (afterDispatch is null)
            {
                ctx.Response.StatusCode = result ? (int)HttpStatusCode.OK : (int)HttpStatusCode.NotFound;
                return;
            }

            await afterDispatch.Invoke(query, result, ctx);
        }, endpoint, auth, roles, policies);

        return this;
    }

    public IDispatcherEndpointsBuilder Post(string path, Func<HttpContext, Task> context = null,
        Action<IEndpointConventionBuilder> endpoint = null, bool auth = false, string roles = null,
        params string[] policies)
    {
        _builder.Post(path, context, endpoint, auth, roles, policies);

        return this;
    }

    public IDispatcherEndpointsBuilder Post<TRequest>(string path, Func<TRequest, HttpContext, Task> beforeDispatch = null,
        Func<TRequest, HttpContext, Task> afterDispatch = null, Action<IEndpointConventionBuilder> endpoint = null,
        bool auth = false, string roles = null,
        params string[] policies)
        where TRequest : class, ICommand
    {
        _builder.Post<TRequest>(path, (cmd, ctx) => BuildCommandContext(cmd, ctx, beforeDispatch, afterDispatch),
            endpoint, auth, roles, policies);

        return this;
    }

    public IDispatcherEndpointsBuilder Put(string path, Func<HttpContext, Task> context = null,
        Action<IEndpointConventionBuilder> endpoint = null, bool auth = false, string roles = null,
        params string[] policies)
    {
        _builder.Put(path, context, endpoint, auth, roles, policies);

        return this;
    }

    public IDispatcherEndpointsBuilder Put<TRequest>(string path, Func<TRequest, HttpContext, Task> beforeDispatch = null,
        Func<TRequest, HttpContext, Task> afterDispatch = null, Action<IEndpointConventionBuilder> endpoint = null,
        bool auth = false, string roles = null,
        params string[] policies)
        where TRequest : class, ICommand
    {
        _builder.Put<TRequest>(path, (cmd, ctx) => BuildCommandContext(cmd, ctx, beforeDispatch, afterDispatch), endpoint,
            auth, roles, policies);

        return this;
    }

    public IDispatcherEndpointsBuilder Patch(string path, Func<HttpContext, Task> context = null,
        Action<IEndpointConventionBuilder> endpoint = null, bool auth = false, string roles = null,
        params string[] policies)
    {
        _builder.Patch(path, context, endpoint, auth, roles, policies);

        return this;
    }

    public IDispatcherEndpointsBuilder Patch<TRequest>(string path, Func<TRequest, HttpContext, Task> beforeDispatch = null,
        Func<TRequest, HttpContext, Task> afterDispatch = null, Action<IEndpointConventionBuilder> endpoint = null,
        bool auth = false, string roles = null,
        params string[] policies)
        where TRequest : class, ICommand
    {
        _builder.Patch<TRequest>(path, (cmd, ctx) => BuildCommandContext(cmd, ctx, beforeDispatch, afterDispatch), endpoint,
            auth, roles, policies);

        return this;
    }

    public IDispatcherEndpointsBuilder Delete(string path, Func<HttpContext, Task> context = null,
        Action<IEndpointConventionBuilder> endpoint = null, bool auth = false, string roles = null,
        params string[] policies)
    {
        _builder.Delete(path, context, endpoint, auth, roles, policies);

        return this;
    }

    public IDispatcherEndpointsBuilder Delete<TQuery>(string path, Func<TQuery, HttpContext, Task> beforeDispatch = null,
        Func<TQuery, HttpContext, Task> afterDispatch = null, Action<IEndpointConventionBuilder> endpoint = null,
        bool auth = false, string roles = null,
        params string[] policies)
        where TQuery : class, ICommand
    {
        _builder.Delete<TQuery>(path, (cmd, ctx) => BuildCommandContext(cmd, ctx, beforeDispatch, afterDispatch),
            endpoint, auth, roles, policies);

        return this;
    }

    private static async Task BuildCommandContext<T>(T command, HttpContext context,
        Func<T, HttpContext, Task> beforeDispatch = null,
        Func<T, HttpContext, Task> afterDispatch = null) where T : class, ICommand
    {
        if (beforeDispatch is not null)
        {
            await beforeDispatch.Invoke(command, context);
        }

        var dispatcher = context.RequestServices.GetRequiredService<ICommandDispatcher>();
        await dispatcher.SendAsync(command);
        if (afterDispatch is null)
        {
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            return;
        }

        await afterDispatch.Invoke(command, context);
    }
}