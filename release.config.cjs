module.exports = {
  branches: ['main'],
  tagFormat: 'v${version}',
  plugins: [
    '@semantic-release/commit-analyzer',
    '@semantic-release/release-notes-generator',
    [
      '@semantic-release/exec',
      {
        prepareCmd: [
          'rm -rf ./nupkgs',
          'python3 scripts/pack-release-packages.py ./nupkgs ${nextRelease.version}',
          'python3 scripts/validate-nuget-packages.py ./nupkgs',
          'python3 scripts/validate-consumer-package-references.py ./nupkgs',
        ].join(' && '),
        publishCmd: [
          'HEXALITH_REQUIRE_CONTAINER_PUBLISHER=true bash scripts/validate-release-secrets.sh',
          'python3 scripts/validate-nuget-packages.py ./nupkgs',
          'python3 scripts/validate-consumer-package-references.py ./nupkgs',
          'dotnet nuget push ./nupkgs/*.nupkg --api-key "$NUGET_API_KEY" --source https://api.nuget.org/v3/index.json --skip-duplicate',
          './.hexalith/release/publish-containers.sh ${nextRelease.version}',
        ].join(' && '),
      },
    ],
    [
      '@semantic-release/github',
      {
        assets: ['nupkgs/*.nupkg'],
      },
    ],
  ],
};
