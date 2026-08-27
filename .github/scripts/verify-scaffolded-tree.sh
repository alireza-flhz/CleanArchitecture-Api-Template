#!/usr/bin/env bash
# Static checks on a freshly scaffolded project. Run from the scaffolded directory,
# BEFORE building, so bin/obj cannot mask a finding.
#
#   verify-scaffolded-tree.sh <ProjectName>
#
# Template-repo only - .template.config/template.json excludes .github/scripts from
# scaffolded projects.
set -uo pipefail

name="${1:?usage: verify-scaffolded-tree.sh <ProjectName>}"
fail=0

# .NET templating derives a namespace-safe variant for names that are not valid C#
# identifiers (my-api -> my_api), so both spellings legitimately appear in the output.
safe_name=$(printf '%s' "$name" | sed 's/[^A-Za-z0-9_.]/_/g')

# True when the argument still mentions the template's own name once every occurrence of
# the chosen name is removed - a chosen name may legitimately contain it (BaseRepositoryClone).
has_leftover() {
  local stripped="${1//$name/}"
  stripped="${stripped//$safe_name/}"
  case "$stripped" in *BaseRepository*) return 0 ;; *) return 1 ;; esac
}

check() {
  if [ "$1" -eq 0 ]; then
    echo "  PASS  $2"
  else
    echo "  FAIL  $2"
    fail=1
  fi
}

echo "== Verifying scaffolded tree for '$name' in $(pwd)"

# 1. sourceName substitution must be total: not one occurrence of the template's own name
#    may survive, in file contents or in any path.
leftover_content=$(grep -rIl "BaseRepository" . 2>/dev/null | while read -r f; do
  has_leftover "$(cat "$f")" && echo "$f"
done)
[ -z "$leftover_content" ]
check $? "no file contains the string 'BaseRepository'"
[ -n "$leftover_content" ] && echo "$leftover_content" | sed 's/^/        /'

leftover_paths=$(find . -name "*BaseRepository*" 2>/dev/null | while read -r p; do
  has_leftover "$p" && echo "$p"
done)
[ -z "$leftover_paths" ]
check $? "no path contains 'BaseRepository'"
[ -n "$leftover_paths" ] && echo "$leftover_paths" | sed 's/^/        /'

# 2. The new name must actually be present - guards against a substitution that
#    deleted rather than replaced.
grep -rIq "$safe_name" . 2>/dev/null
check $? "the scaffolded name (as '$safe_name') appears in the tree"

# 3. Solution wiring: 8 projects, every one renamed.
sln="$safe_name.sln"
[ -f "$sln" ]
check $? "$sln exists"

if [ -f "$sln" ]; then
  projects=$(dotnet sln "$sln" list | grep -c '\.csproj' || true)
  [ "$projects" -eq 8 ]
  check $? "solution lists 8 projects (found $projects)"

  unrenamed=$(dotnet sln "$sln" list | grep '\.csproj' | grep -vc "$safe_name" || true)
  [ "$unrenamed" -eq 0 ]
  check $? "every project in the solution is renamed (found $unrenamed unrenamed)"

  # The whole point of normalising the name: what the solution references must exist on disk.
  missing=0
  while read -r proj; do
    path=$(echo "$proj" | tr '\\' '/')
    [ -f "$path" ] || { echo "        solution references a missing project: $path"; missing=1; }
  done < <(dotnet sln "$sln" list | grep '\.csproj')
  [ "$missing" -eq 0 ]
  check $? "every project the solution references exists on disk"
fi

# 4. Template-repo-only workflows must not reach consumers; ci.yml must.
[ -f .github/workflows/ci.yml ]
check $? ".github/workflows/ci.yml was scaffolded"

extra=$(ls .github/workflows | grep -v '^ci\.yml$' || true)
[ -z "$extra" ]
check $? "no template-only workflow was scaffolded"
[ -n "$extra" ] && echo "$extra" | sed 's/^/        /'

[ ! -d .github/scripts ]
check $? "no template-only scripts were scaffolded"

[ ! -d .template.config ]
check $? "the template definition itself was not scaffolded"

# 5. No secret and no developer database may ship.
key=$(grep -o '"SigningKey": *"[^"]*"' src/Api/appsettings.json || true)
[ "$key" = '"SigningKey": ""' ]
check $? "appsettings.json ships a blank SigningKey (found: ${key:-none})"

[ -z "$(find . -name '*.db' -o -name '*.db-shm' -o -name '*.db-wal' 2>/dev/null)" ]
check $? "no SQLite database file was scaffolded"

[ -z "$(find . -type d \( -name bin -o -name obj \) 2>/dev/null)" ]
check $? "no bin/obj directory was scaffolded"

# 6. Namespaces must be legal C#. dotnet new does not reject a name that is not a valid
#    identifier, so a hyphenated name would silently produce uncompilable namespaces.
namespace=$(grep -h '^namespace ' src/Domain/Entities/BaseEntity.cs | head -1)
echo "$namespace" | grep -Eq '^namespace [A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)*;?$'
check $? "root namespace is a valid C# identifier ($namespace)"

echo
[ "$fail" -eq 0 ] && echo "== Tree verification passed" || echo "== Tree verification FAILED"
exit "$fail"
