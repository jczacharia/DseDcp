// Copyright (c) PNC Financial Services. All rights reserved.

using System.CodeDom.Compiler;
using System.Reflection;
using Vogen;

[assembly: VogenDefaults(
    throws: null,
    underlyingType: null,
    conversions: Conversions.SystemTextJson | Conversions.TypeConverter,
    customizations: Customizations.AddFactoryMethodForGuids,
    deserializationStrictness: DeserializationStrictness.Default,
    debuggerAttributes: DebuggerAttributeGeneration.Basic,
    toPrimitiveCasting: CastOperator.Explicit,
    fromPrimitiveCasting: CastOperator.Explicit,
    disableStackTraceRecordingInDebug: false,
    parsableForStrings: ParsableForStrings.GenerateMethodsAndInterface,
    parsableForPrimitives: ParsableForPrimitives.HoistMethodsAndInterfaces,
    tryFromGeneration: TryFromGeneration.GenerateBoolAndErrorOrMethods,
    isInitializedMethodGeneration: IsInitializedMethodGeneration.Generate,
    systemTextJsonConverterFactoryGeneration: SystemTextJsonConverterFactoryGeneration.Generate,
    staticAbstractsGeneration: StaticAbstractsGeneration.ExplicitCastFromPrimitive
        | StaticAbstractsGeneration.ExplicitCastToPrimitive
        | StaticAbstractsGeneration.EqualsOperators
        | StaticAbstractsGeneration.FactoryMethods
        | StaticAbstractsGeneration.InstanceMethodsAndProperties
        | StaticAbstractsGeneration.ValueObjectsDeriveFromTheInterface
        | StaticAbstractsGeneration.InstancesHaveInterfaceDefinition,
    openApiSchemaCustomizations: OpenApiSchemaCustomizations.GenerateOpenApiMappingExtensionMethod,
    explicitlySpecifyTypeInValueObject: true,
    primitiveEqualityGeneration: PrimitiveEqualityGeneration.GenerateOperatorsAndMethods,
    numericsGeneration: NumericsGeneration.Omit
)]

namespace Dse.Extensions;

public static class VogenStructExtensions
{
    extension<TW, TP>(IVogen<TW, TP>)
        where TW : struct, IVogen<TW, TP>
        where TP : struct
    {
        public static TW? FromNullable(TP? value) => value is null ? null : TW.From(value.Value);
    }

    extension<TW, TP>(IVogen<TW, TP>)
        where TW : struct, IVogen<TW, TP>
        where TP : class
    {
        public static TW? FromNullable(TP? value) => value is null ? null : TW.From(value);
    }
}

public static class VogenExtensions
{
    extension<TW, TP>(IVogen<TW, TP>)
        where TW : class, IVogen<TW, TP>
        where TP : struct
    {
        public static TW? FromNullable(TP? value) => value is null ? null : TW.From(value.Value);
    }

    extension<TW, TP>(IVogen<TW, TP>)
        where TW : class, IVogen<TW, TP>
        where TP : class
    {
        public static TW? FromNullable(TP? value) => value is null ? null : TW.From(value);
    }

    extension(Type targetType)
    {
        public bool IsVogenValueObject()
        {
            var generatedCode = targetType.GetCustomAttribute<GeneratedCodeAttribute>();
            return generatedCode is { Tool: "Vogen" };
        }
    }
}
