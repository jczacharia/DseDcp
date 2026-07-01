// Copyright (c) PNC Financial Services. All rights reserved.

using Elastic.Mapping;

namespace Dse.Confluence;

[ElasticsearchMappingContext]
[Index<ConfluenceDoc>(
    Name = "source-confluence",
    WriteAlias = "source-confluence",
    ReadAlias = "source-confluence-search",
    DatePattern = "yyyy.MM.dd.HHmmss",
    RefreshInterval = "30s",
    Configuration = typeof(ConfluenceDocConfiguration)
)]
[Index<ConfluenceDoc>(
    NameTemplate = "test-confluence-{uuid}",
    RefreshInterval = "-1",
    Variant = "Test",
    Configuration = typeof(ConfluenceDocConfiguration)
)]
public static partial class ConfluenceMappingContext;
