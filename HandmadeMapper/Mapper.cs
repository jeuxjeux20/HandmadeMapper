﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using HandmadeMapper.ExpressionProcessing;

namespace HandmadeMapper
{
    /// <summary>
    ///     The default mapper class, that applies processing transformations to mapping expressions.
    /// </summary>
    /// <seealso cref="IExpressionTransformer" />
    /// <typeparam name="TInput">The input type the mapper is using.</typeparam>
    /// <typeparam name="TResult">The result type the mapper will give.</typeparam>
    public class Mapper<TInput, TResult> : IMapper<TInput, TResult>
    {
        private Expression<Func<TInput, TResult>>? _actualExpression;
        private Lazy<Func<TInput, TResult>>? _compiledExpression;

        /// <summary>
        ///     Creates a mapper with the specified <paramref name="mapperOptions" />.
        /// </summary>
        /// <param name="mapperOptions">The mapper options to use.</param>
        /// <param name="expressionTransformers">
        ///     The expression transformers to use. Defaults to <see cref="Mapper.DefaultExpressionTransformers" />.
        ///     If <paramref name="mergeWithDefault" /> is true, the items are merged with
        ///     <see cref="Mapper.DefaultExpressionTransformers" />.
        /// </param>
        /// <param name="mergeWithDefault">
        ///     Defines if the <paramref name="expressionTransformers" />
        ///     should be merged with the <see cref="Mapper.DefaultExpressionTransformers" />.
        /// </param>
        public Mapper(IMapperOptions<TInput, TResult> mapperOptions,
            IEnumerable<IExpressionTransformer>? expressionTransformers = null,
            bool mergeWithDefault = false) : this(expressionTransformers, mergeWithDefault)
        {
            if (mapperOptions == null)
                throw new ArgumentNullException(nameof(mapperOptions));

            IsLazy = mapperOptions.IsLazy;
            Expression = mapperOptions.Expression;
        }

        /// <summary>
        ///     Creates a mapper with the specified <paramref name="expression" />.
        /// </summary>
        /// <param name="expression">The mapping expression to use.</param>
        /// <param name="expressionTransformers">
        ///     The expression transformers to use. Defaults to <see cref="Mapper.DefaultExpressionTransformers" />.
        ///     If <paramref name="mergeWithDefault" /> is true, the items are merged with
        ///     <see cref="Mapper.DefaultExpressionTransformers" />.
        /// </param>
        /// <param name="mergeWithDefault">
        ///     Defines if the <paramref name="expressionTransformers" />
        ///     should be merged with the <see cref="Mapper.DefaultExpressionTransformers" />.
        /// </param>
        public Mapper(Expression<Func<TInput, TResult>> expression,
            IEnumerable<IExpressionTransformer>? expressionTransformers = null,
            bool mergeWithDefault = false) : this(expressionTransformers, mergeWithDefault)
        {
            Expression = expression ?? throw new ArgumentNullException(nameof(expression));
        }

        /// <summary>
        ///     Creates a mapper with no expression. The expression should be set using <see cref="UseExpression" />.
        /// </summary>
        /// <param name="expressionTransformers">
        ///     The expression transformers to use. Defaults to <see cref="Mapper.DefaultExpressionTransformers" />.
        ///     If <paramref name="mergeWithDefault" /> is true, the items are merged with
        ///     <see cref="Mapper.DefaultExpressionTransformers" />.
        /// </param>
        /// <param name="mergeWithDefault">
        ///     Defines if the <paramref name="expressionTransformers" />
        ///     should be merged with the <see cref="Mapper.DefaultExpressionTransformers" />.
        /// </param>
        protected Mapper(IEnumerable<IExpressionTransformer>? expressionTransformers = null,
            bool mergeWithDefault = false)
        {
            ExpressionTransformers =
                (mergeWithDefault
                    ? Mapper.DefaultExpressionTransformers.Concat(expressionTransformers)
                    : expressionTransformers ?? Mapper.DefaultExpressionTransformers).ToList();
        }

        /// <summary>
        ///     <para>
        ///         Creates a mapper with no expression. The expression should be set using <see cref="UseExpression" />.
        ///     </para>
        ///     <para>
        ///         <b>This is the recommended constructor to use when inheriting this class and using dependency injection.</b>
        ///     </para>
        /// </summary>
        /// <param name="expressionTransformers">
        ///     The expression transformers to use. Defaults to
        ///     <see cref="Mapper.DefaultExpressionTransformers" />.
        /// </param>
        protected Mapper(IEnumerable<IExpressionTransformer>? expressionTransformers = null)
            : this(expressionTransformers, false)
        {
        }

        /// <summary>
        ///     Defines whether or not the expression should be transformed lazily (on get) or eagerly (on set).
        /// </summary>
        protected bool IsLazy { get; set; }

        /// <summary>
        ///     The expression processors used to transform the expression.
        /// </summary>
        protected IEnumerable<IExpressionTransformer> ExpressionTransformers { get; }

        /// <inheritdoc />
        public Expression<Func<TInput, TResult>> OriginalExpression { get; private set; } = null!;

        /// <inheritdoc />
        public Expression<Func<TInput, TResult>> Expression
        {
            get
            {
                _actualExpression ??= ApplyExpressionTransformers(OriginalExpression);
                return _actualExpression;
            }
            // Can be lazy initialized (UseExpression)
            private set
            {
                if (value is null) throw new ArgumentNullException(nameof(value));
                if (value == OriginalExpression) return;

                OriginalExpression = value;
                if (!IsLazy)
                    ApplyExpressionTransformers(OriginalExpression);
                _compiledExpression = new Lazy<Func<TInput, TResult>>(() => Expression.Compile());
            }
        }

        /// <inheritdoc />
        LambdaExpression IMapperExpressionProvider.Expression => Expression;

        /// <inheritdoc />
        public TResult Map(TInput source)
        {
            if (_compiledExpression is null) throw new InvalidOperationException("There is no expression set up.");
            return _compiledExpression.Value(source);
        }

        /// <inheritdoc />
        public virtual IMapper<TInput, TResult> WithExpression(Expression<Func<TInput, TResult>> expression)
        {
            var mapperOptions = CreateMapperOptionsBuilder().WithExpression(expression).Build();

            return new Mapper<TInput, TResult>(mapperOptions, ExpressionTransformers);
        }

        /// <summary>
        ///     Sets the mapper's <see cref="Expression" /> to the specified <paramref name="expression" />,
        ///     <b>only when no expression has been given</b>.
        /// </summary>
        /// <param name="expression">The expression to use.</param>
        /// <exception cref="InvalidOperationException">When an exception is already present.</exception>
        /// <seealso cref="Mapper{TInput,TResult}(IEnumerable{IExpressionTransformer}, bool)" />
        protected void UseExpression(Expression<Func<TInput, TResult>> expression)
        {
            if (Expression != null)
                throw new InvalidOperationException("An expression is already present.");

            Expression = expression ?? throw new ArgumentNullException(nameof(expression));
        }

        private Expression<T> ApplyExpressionTransformers<T>(Expression<T> expr, MappingContext? context = null)
        {
            context ??= new MappingContext(typeof(TInput), typeof(TResult), this);

            foreach (var expressionTransformer in ExpressionTransformers)
            {
                expr = expressionTransformer.Transform(expr, context);
            }

            return expr;
        }

        /// <summary>
        ///     Creates a <see cref="MapperOptionsBuilder{TInput,TResult}" /> instance from the configuration of this mapper.
        /// </summary>
        /// <returns>A <see cref="MapperOptionsBuilder{TInput,TResult}" /> with the configuration of this mapper</returns>
        private MapperOptionsBuilder<TInput, TResult> CreateMapperOptionsBuilder()
        {
            return MapperOptionsBuilder.Create(Expression)
                .WithIsLazy(IsLazy);
        }
    }

    /// <summary>
    ///     Provides default behavior for <see cref="Mapper{TInput,TResult}" /> and placeholder methods for expression
    ///     transformations.
    /// </summary>
    /// <seealso cref="Mapper{TInput,TResult}" />
    [SuppressMessage("Style", "IDE0060:Remove unused parameter",
        Justification = "Those are used in expressions.")]
    public static class Mapper
    {
        private const string IncludeError =
            "This method should only be used in expressions, or the IncludeExpressionTransformer has not been used.";

        /// <summary>
        ///     <para>
        ///         The default expression transformers to use with the <see cref="Mapper{TInput,TResult}" />.
        ///     </para>
        ///     <para>
        ///         By default, this list contains, in order:
        ///         <list type="bullet">
        ///             <item>
        ///                 An
        ///                 <b>
        ///                     <see cref="IncludeExpressionTransformer" />
        ///                 </b>
        ///                 , without any <see cref="IMapperResolver" />s.
        ///             </item>
        ///         </list>
        ///     </para>
        /// </summary>
        public static readonly List<IExpressionTransformer> DefaultExpressionTransformers
            = new List<IExpressionTransformer>
            {
                new IncludeExpressionTransformer(Array.Empty<IMapperResolver>()),
                new MapWithExpressionTransformer()
            };

#pragma warning disable CA1801 // Review unused parameters
        /// <summary>
        ///     <para>
        ///         Includes the specified <paramref name="mapper" />'s expression, using the specified <paramref name="source" />.
        ///     </para>
        ///     <para>
        ///         This method call <c>Method.Include(x.Whatever, someMapper)</c>
        ///         will be replaced by the <paramref name="mapper" />'s expression (
        ///         <see cref="IMapperExpressionProvider.Expression" />),
        ///         and the input parameter replaced with the <paramref name="source" /> instead.
        ///     </para>
        ///     <para>
        ///         <b>NOTE: This requires the <see cref="IncludeExpressionTransformer" /> to be applied.</b>
        ///     </para>
        /// </summary>
        /// <example>
        ///     We have the following mapper:
        ///     <code>
        /// var thingMapper = CreateSomeMapper(x =&gt; new Thing
        /// {
        ///     Id = x.Id,
        ///     Name = x.Name
        /// });
        /// </code>
        ///     So, this:
        ///     <code>
        /// x =&gt; new World 
        /// {
        ///     BestThing = Mapper.Include(x.BestThing, thingMapper)
        /// }
        /// </code>
        ///     Results in:
        ///     <code>
        /// x =&gt; new World 
        /// {
        ///     BestThing = x =&gt; new Thing
        ///     {
        ///         Id = x.BestThing.Id,
        ///         Name = x.BestThing.Name
        ///     }
        /// }
        /// </code>
        /// </example>
        /// <typeparam name="TInput">The input type of the mapper.</typeparam>
        /// <typeparam name="TResult">The result type of the mapper.</typeparam>
        /// <param name="source">The source object to use in the mapper.</param>
        /// <param name="mapper">The mapper to use.</param>
        /// <returns>The result of the <paramref name="mapper" />, with the given <paramref name="source" />.</returns>
        public static TResult Include<TInput, TResult>(TInput source, IMapper<TInput, TResult> mapper)
        {
            throw new InvalidOperationException(IncludeError);
        }

        /// <summary>
        ///     <para>
        ///         Includes the specified <paramref name="expression" />, with the input parameter being the
        ///         <paramref name="source" />.
        ///     </para>
        ///     <para>
        ///         This expression method call <c>Method.Include(x.Whatever, someExpression)</c>,
        ///         will be replaced by the <paramref name="expression" />, and the input parameter replaced with the
        ///         <paramref name="source" /> instead.
        ///     </para>
        ///     <para>
        ///         <b>NOTE: This requires the <see cref="IncludeExpressionTransformer" /> to be applied.</b>
        ///     </para>
        /// </summary>
        /// <typeparam name="TInput">The input type of the expression.</typeparam>
        /// <typeparam name="TResult">The result type of the expression.</typeparam>
        /// <param name="source">The source object to use as the expression's first parameter.</param>
        /// <param name="expression">The mapper to use.</param>
        /// <returns>The <paramref name="expression" />, with the <paramref name="source" /> instead of its original parameter.</returns>
        public static TResult Include<TInput, TResult>(TInput source, Expression<Func<TInput, TResult>> expression)
        {
            throw new InvalidOperationException(IncludeError);
        }

        /// <summary>
        ///     Does the same as <see cref="Include{TInput,TResult}(TInput,IMapper{TInput,TResult})" />,
        ///     but tries to find a mapper using the <see cref="IMapperResolver" />s given to the
        ///     <see cref="IncludeExpressionTransformer" />.
        ///     <para>
        ///         <b>NOTE: This requires the <see cref="IncludeExpressionTransformer" /> to be applied.</b>
        ///     </para>
        /// </summary>
        /// <typeparam name="TInput">The input type of the mapper.</typeparam>
        /// <typeparam name="TResult">The result type of the mapper.</typeparam>
        /// <param name="source">The source object to use in the mapper.</param>
        /// <returns>The result of the mapper, with the given <paramref name="source" />.</returns>
        /// <exception cref="InvalidOperationException">When the mapper couldn't get resolved.</exception>
        public static TResult Include<TInput, TResult>(TInput source)
        {
            throw new InvalidOperationException(IncludeError);
        }
#pragma warning restore CA1801 // Review unused parameters
    }
}