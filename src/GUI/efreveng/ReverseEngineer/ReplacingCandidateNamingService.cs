using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Scaffolding.Internal;
using Microsoft.EntityFrameworkCore.Scaffolding.Metadata;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ReverseEngineer20.ReverseEngineer
{
    public class ReplacingCandidateNamingService : CandidateNamingService
    {
        private readonly ReverseEngineerCommandOptions _customOptions;
        private readonly List<Schema> _customNameOptions;

        public ReplacingCandidateNamingService(ReverseEngineerCommandOptions options)
        {
            _customOptions = options;
            _customNameOptions = options.CustomReplacers;
        }


        public override string GenerateCandidateIdentifier(DatabaseTable originalTable)
        {
            if (originalTable == null)
            {
                throw new ArgumentException("Argument is empty", nameof(originalTable));
            }

            var candidateStringBuilder = new StringBuilder();

            var schema = GetSchema(originalTable.Schema);

            if (schema == null)
            {
                return base.GenerateCandidateIdentifier(originalTable);
            }

            if (schema.UseSchemaName)
            {
                candidateStringBuilder.Append(ToPascalCase(originalTable.Schema));
                candidateStringBuilder.Append(ToPascalCase(originalTable.Name));

                return candidateStringBuilder.ToString();
            }
            else if (schema.Tables.Count > 0)
            {
                var newTableName = _customNameOptions
                    .FirstOrDefault(x => x.SchemaName == schema.SchemaName)
                    .Tables?
                    .FirstOrDefault(t => t.Name == originalTable.Name)?.NewName;

                if (string.IsNullOrWhiteSpace(newTableName))
                {
                    candidateStringBuilder.Append(ToPascalCase(originalTable.Name));

                    return candidateStringBuilder.ToString();
                }

                candidateStringBuilder.Append(newTableName);

                return candidateStringBuilder.ToString();
            }
            else
            {
                return base.GenerateCandidateIdentifier(originalTable);
            }
        }

        public override string GenerateCandidateIdentifier(DatabaseColumn originalColumn)
        {
            var temp = String.Empty;
            var candidateStringBuilder = new StringBuilder();

            var schema = this.GetSchema(originalColumn.Table.Schema);

            if (schema == null)
            {
                return base.GenerateCandidateIdentifier(originalColumn);
            }

            if (schema.Tables == null)
            {
                return base.GenerateCandidateIdentifier(originalColumn);
            }

            var columns = this._customNameOptions
                .FirstOrDefault(s => s.SchemaName == schema.SchemaName)?
                .Tables?
                .FirstOrDefault(t => t.Name == originalColumn.Table.Name)?
                .Columns?
                .FirstOrDefault(c => c.Name == originalColumn.Name);


            if (columns != null)
            {
                candidateStringBuilder.Append(columns.NewName);
                return candidateStringBuilder.ToString();
            }
            else
            {
                return base.GenerateCandidateIdentifier(originalColumn);
            }
        }

        public override string GetDependentEndCandidateNavigationPropertyName(IForeignKey foreignKey)
        {
            var baseName = base.GetDependentEndCandidateNavigationPropertyName(foreignKey);
            var originalSchema = foreignKey.PrincipalEntityType.GetSchema();

            //var originalTable = foreignKey.DeclaringEntityType.Scaffolding().TableName;
            var schema = this.GetSchema(originalSchema);

            if (schema == null)
            {
                baseName = base.GetDependentEndCandidateNavigationPropertyName(foreignKey);
            }
            else if (foreignKey.IsSelfReferencing())
            {
                baseName = base.GetDependentEndCandidateNavigationPropertyName(foreignKey);
            }
            else if (schema.SchemaName == originalSchema && schema.UseSchemaName)
            {
                baseName = ToPascalCase(schema.SchemaName) + baseName;
            }
            else
            {
                baseName = base.GetDependentEndCandidateNavigationPropertyName(foreignKey);
            }
            
            // Managae RegEx extraction
            if (!String.IsNullOrWhiteSpace(_customOptions.ForeignKeyNameFormat))
            {
                var matcher = Regex.Match(baseName, this._customOptions.ForeignKeyNameFormat);

                if (matcher != null && matcher.Success && matcher.Length == 2)
                {
                    return baseName.Remove(matcher.Captures[0].Index, matcher.Captures[0].Length);
                }
            }

            // Done
            return baseName;
        }

        public override string GetPrincipalEndCandidateNavigationPropertyName(IForeignKey foreignKey, string dependentEndNavigationPropertyName)
        {
            if (this._customOptions.UseInflectorForExternalNavigation)
            {
                var pluralizer = new InflectorPluralizer();
                var pluralized = pluralizer.Pluralize(base.GetPrincipalEndCandidateNavigationPropertyName(foreignKey, dependentEndNavigationPropertyName));
                return pluralized;
            }
            else if (this._customOptions.UseLegacyPluralizerForExternalNavigation)
            {
                var pluralizer = new LegacyInflectorPluralizer();
                var pluralized = pluralizer.Pluralize(base.GetPrincipalEndCandidateNavigationPropertyName(foreignKey, dependentEndNavigationPropertyName));
                return pluralized;
            }
            else
            {
                return base.GetPrincipalEndCandidateNavigationPropertyName(foreignKey, dependentEndNavigationPropertyName);
            }

        }

        private Schema GetSchema(string originalSchema)
            => this._customNameOptions?
                    .FirstOrDefault(x => x.SchemaName == originalSchema);

        private static string ToPascalCase(string value)
        {

            var candidateStringBuilder = new StringBuilder();
            var previousLetterCharInWordIsLowerCase = false;
            var isFirstCharacterInWord = true;

            foreach (var c in value)
            {
                var isNotLetterOrDigit = !Char.IsLetterOrDigit(c);
                if (isNotLetterOrDigit
                    || (previousLetterCharInWordIsLowerCase && Char.IsUpper(c)))
                {
                    isFirstCharacterInWord = true;
                    previousLetterCharInWordIsLowerCase = false;
                    if (isNotLetterOrDigit)
                    {
                        continue;
                    }
                }

                candidateStringBuilder.Append(
                    isFirstCharacterInWord ? Char.ToUpperInvariant(c) : Char.ToLowerInvariant(c));
                isFirstCharacterInWord = false;
                if (Char.IsLower(c))
                {
                    previousLetterCharInWordIsLowerCase = true;
                }
            }

            return candidateStringBuilder.ToString();
        }
    }
}
