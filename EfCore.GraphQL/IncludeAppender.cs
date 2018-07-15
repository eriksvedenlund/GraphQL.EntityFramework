﻿using System.Collections.Generic;
using System.Linq;
using GraphQL.Language.AST;
using GraphQL.Types;
using Microsoft.EntityFrameworkCore;

namespace EfCoreGraphQL
{
    public static class IncludeAppender
    {
        public static IQueryable<TItem> AddIncludes<TItem, TSource>(IQueryable<TItem> query, ResolveFieldContext<TSource> context)
            where TItem : class
        {
            return AddIncludes(query, context.FieldDefinition, context.SubFields.Values);
        }

        static IQueryable<T> AddIncludes<T>(IQueryable<T> query, FieldType fieldType, ICollection<Field> subFields)
            where T : class
        {
            foreach (var path in GetPaths(fieldType, subFields))
            {
                query = query.Include(path);
            }

            return query;
        }

        public static IEnumerable<string> GetPaths(FieldType fieldType, ICollection<Field> fields)
        {
            var list = new List<string>();

            var complexGraph= fieldType.GetComplexGraph();
            ProcessSubFields(list, null, fields, complexGraph);
            return list;
        }

        static void AddField(List<string> list, Field field, string parentPath, FieldType fieldType)
        {
            if (!fieldType.TryGetComplexGraph(out var complexGraph))
            {
                return;
            }

            var subFields = field.SelectionSet.Selections.OfType<Field>().ToList();
            if (IsConnectionNode(field))
            {
                if (subFields.Any())
                {
                    ProcessSubFields(list, parentPath, subFields, complexGraph);
                }

                return;
            }

            var path = GetPath(parentPath, field, fieldType);
            if (subFields.Any())
            {
                list.Add(path);
                ProcessSubFields(list, path, subFields, complexGraph);
            }
        }

        static void ProcessSubFields(List<string> list, string parentPath, ICollection<Field> subFields, IComplexGraphType complexGraph)
        {
            foreach (var subField in subFields)
            {
                var single = complexGraph.Fields.SingleOrDefault(x => x.Name == subField.Name);
                if (single != null)
                {
                    AddField(list, subField, parentPath, single);
                }
            }
        }


        static bool IsConnectionNode(Field field)
        {
            var name = field.Name;
            return name == "edges" || name == "items" || name == "node";
        }

        static string GetPath(string parentPath, Field field, FieldType fieldType)
        {
            var fieldName = GetFieldName(field, fieldType);

            if (parentPath == null)
            {
                return fieldName;
            }

            return $"{parentPath}.{fieldName}";
        }

        static string GetFieldName(Field field, FieldType fieldType)
        {
            if (fieldType != null)
            {
                if (fieldType.Metadata.TryGetValue("IncludeName", out var fieldNameObject))
                {
                    return (string) fieldNameObject;
                }
            }

            return char.ToUpperInvariant(field.Name[0]) + field.Name.Substring(1);
        }
    }
}