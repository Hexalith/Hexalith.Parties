# DW-182 / I16 exact identity comparison

Administrator / jpiquot first approved the ten committed source identities at
Parties `a92d15866bbf4f49cefa6c03f4a9eeec442af1de`, including Tenants
`4b3e431745550f8466f1ce0d70e6b507c747b59a`, and the effective Release
graph for current-selection assertions on 2026-11-02. That Tenants identity is
dated history after Parties `18579b62ef9ac3c8780fc5e8ba26112d2665f566`
selected `4d0d599879767196928e1c265622483a62b457ae`. Presented that exact
successor SHA, the owner answered "push all changes" on 2026-11-02, approving
its current-selection assertion and gitlink signoff. The other nine gitlinks
and all seven effective package versions are unchanged. Both decisions are
selection only: all six I16 receipts remain Unvalidated until rerun at their
stamped identities; neither grants publication, deployment, custody, or
rollback deletion authority.

On 2026-11-02 the spec 8.10 Round 9 review presented Tenants
`4d0d599879767196928e1c265622483a62b457ae` with the option to confirm it
explicitly; the owner replied "apply recommended", confirming the successor's
current-selection assertion and gitlink signoff with that review as the reviewer
gate. The confirmation changes no identity and validates no parity receipt.

### Initial comparison (dated history): 2026-11-01 approved -> committed Parties a92d15866bbf4f49cefa6c03f4a9eeec442af1de (same ten gitlinks approved from b51a9db03194f31afdb30a056c4116f2c755a0bf)

| Root gitlink | Approved SHA | Current SHA |
| --- | --- | --- |
| AI.Tools | `9b78aa008cfc62cd88ad0ea4b39b1f07311956ce` | `1745b6663ad32bb1288167135ed0f979366c53a0` |
| Builds | `df13d9d31f7ee1a1b0fb7eeac7e8b27b9a9f9fb0` | `d2a430c988462a634674a69c562ea4afd1f06ef4` |
| Commons | `bbbce518bcca9ca359762393688f02d142af1c61` | `11555ace9efcae2928a00122dcba91243e5a8a1a` |
| EventStore | `ba4c5548ad260b049bda5a1c7def2e8af3484733` | `7c93a1234d14c66f5ee85ed48caaee1d30a671f3` |
| FrontComposer | `c092e8f91ebb670fad4ab959f2d3c8e13d809e0f` | `07089dcbdad784a1d53037e9cea78d1d7becd8d6` |
| McpCli | `17eb4fbbc151ca4b99dfa452e9f65655db2b402f` | `3bc89642ad43305531c4018b75b5e1b9e0681e10` |
| Memories | `b93b32cf77f3d76e51d3221550eff99f4d1e93cb` | `e8956eb5c1bd49297f86702f9606eb9b013bc2f1` |
| Platform | `03799ed553a16c8a1528068ba4d95bb5509ea75e` | `92f87e6a49e4357f91b2ffb821e044931291c0e6` |
| PolymorphicSerializations | `821b784bb51483acefa9725acc6792d111304a4e` | `bce4eb9f45c6b131b960ab4fa39e50ff01e51960` |
| Tenants | `2d3fa6da2312c1b5cf7f5c9461cc2d0ffc995b43` | `4b3e431745550f8466f1ce0d70e6b507c747b59a` |

### Exact source identity change: 2026-11-01 approved -> committed Parties 18579b62ef9ac3c8780fc5e8ba26112d2665f566

| Root gitlink | Approved SHA | Current SHA |
| --- | --- | --- |
| AI.Tools | `9b78aa008cfc62cd88ad0ea4b39b1f07311956ce` | `1745b6663ad32bb1288167135ed0f979366c53a0` |
| Builds | `df13d9d31f7ee1a1b0fb7eeac7e8b27b9a9f9fb0` | `d2a430c988462a634674a69c562ea4afd1f06ef4` |
| Commons | `bbbce518bcca9ca359762393688f02d142af1c61` | `11555ace9efcae2928a00122dcba91243e5a8a1a` |
| EventStore | `ba4c5548ad260b049bda5a1c7def2e8af3484733` | `7c93a1234d14c66f5ee85ed48caaee1d30a671f3` |
| FrontComposer | `c092e8f91ebb670fad4ab959f2d3c8e13d809e0f` | `07089dcbdad784a1d53037e9cea78d1d7becd8d6` |
| McpCli | `17eb4fbbc151ca4b99dfa452e9f65655db2b402f` | `3bc89642ad43305531c4018b75b5e1b9e0681e10` |
| Memories | `b93b32cf77f3d76e51d3221550eff99f4d1e93cb` | `e8956eb5c1bd49297f86702f9606eb9b013bc2f1` |
| Platform | `03799ed553a16c8a1528068ba4d95bb5509ea75e` | `92f87e6a49e4357f91b2ffb821e044931291c0e6` |
| PolymorphicSerializations | `821b784bb51483acefa9725acc6792d111304a4e` | `bce4eb9f45c6b131b960ab4fa39e50ff01e51960` |
| Tenants | `2d3fa6da2312c1b5cf7f5c9461cc2d0ffc995b43` | `4d0d599879767196928e1c265622483a62b457ae` |

Effective Release package changes: EventStore `3.130.2 -> 3.132.2`, Commons `2.30.8 -> 2.30.10`, PolymorphicSerializations `1.19.7 -> 1.19.8`. FrontComposer `4.7.4`, Memories `3.0.13`, Tenants `6.0.0`, and McpCli `1.1.0` remain selected. The current Builds catalog defaults EventStore `3.132.1`, FrontComposer `4.7.5`, and Memories `3.0.15`; Parties root pins select the values above. EventStore source SHA is the remote `v3.132.2` tag; that tag is absent locally, where `git describe` gives `v3.132.1-2-g7c93a123`.
