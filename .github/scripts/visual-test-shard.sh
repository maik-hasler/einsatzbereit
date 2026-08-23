#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -lt 2 ]; then
	echo "usage: $0 <shard-index> <shard-count> [tests-dir]" >&2
	exit 2
fi

shard_index=$1
shard_count=$2
tests_dir=${3:-backend/tests/VisualTests}

case $shard_index in
	'' | *[!0-9]*) echo "::error::shard-index must be a positive integer, got '$shard_index'" >&2; exit 2 ;;
esac
case $shard_count in
	'' | *[!0-9]*) echo "::error::shard-count must be a positive integer, got '$shard_count'" >&2; exit 2 ;;
esac
if [ "$shard_index" -lt 1 ] || [ "$shard_count" -lt 1 ] || [ "$shard_index" -gt "$shard_count" ]; then
	echo "::error::shard-index $shard_index is out of range for shard-count $shard_count" >&2
	exit 2
fi
if [ ! -d "$tests_dir" ]; then
	echo "::error::tests directory '$tests_dir' does not exist" >&2
	exit 2
fi

discover() {
	awk '
		function flush() {
			if (curbase != "" && cases > 0) {
				if (!seenClass) {
					printf("::error::%s declares [Test] methods but no `class %s` - visual-test-shard.sh filters by class name, so this file would be silently excluded from every shard.\n", curfile, curbase) > "/dev/stderr"
					err = 1
				}
				printf("%d\t%s\n", cases, curbase)
			}
		}
		FNR == 1 {
			flush()
			curfile = FILENAME
			n = split(FILENAME, parts, "/")
			curbase = parts[n]
			sub(/\.cs$/, "", curbase)
			cases = 0; inblock = 0; hasTest = 0; args = 0; seenClass = 0
		}
		$0 ~ ("(^|[^A-Za-z0-9_])class[ \t]+" curbase "([^A-Za-z0-9_]|$)") { seenClass = 1 }
		# Blank and comment lines inside an attribute block must not end it -
		# a comment between [Test] and [Arguments] would otherwise split one
		# block in two and undercount the class.
		/^[[:space:]]*$/ { next }
		/^[[:space:]]*\/\// { next }
		/^[[:space:]]*\[/ {
			if (!inblock) { inblock = 1; hasTest = 0; args = 0 }
			if ($0 ~ /\[Test\]/) hasTest = 1
			if ($0 ~ /\[Arguments/) args++
			next
		}
		{ if (inblock) { if (hasTest) cases += (args > 0 ? args : 1); inblock = 0 } }
		END { flush(); if (err) exit 1 }
	' "$tests_dir"/*.cs
}

packed=$(discover | sort -k1,1nr -k2,2 | awk -v shard="$shard_index" -v shards="$shard_count" '
	{ cases[NR] = $1; name[NR] = $2 }
	END {
		if (NR == 0) {
			print "::error::no VisualTests classes discovered - the filter would be empty" > "/dev/stderr"
			exit 1
		}
		for (b = 1; b <= shards; b++) { load[b] = 0; count[b] = 0 }
		for (i = 1; i <= NR; i++) {
			best = 1
			for (b = 2; b <= shards; b++)
				if (load[b] < load[best]) best = b
			load[best] += cases[i]
			count[best]++
			members[best] = members[best] (members[best] == "" ? "" : "|") "(" name[i] ")"
		}
		if (count[shard] == 0) {
			printf("::error::shard %d of %d got no classes - use fewer shards than the %d test classes\n", shard, shards, NR) > "/dev/stderr"
			exit 1
		}
		floor = int(load[shard] * 3 / 4)
		if (floor < 1) floor = 1
		printf("filter=/*/*/%s/*\n", members[shard])
		printf("expected-tests=%d\n", load[shard])
		printf("min-tests=%d\n", floor)
		printf("classes=%d\n", count[shard])
	}
')

echo "$packed"
