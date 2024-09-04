# Project Origin Verifier for Electricity

This Helm chart enables one to install the Energy Track and Trace verifier for elecitricty.

## Configuration

The chart must be configured using values file, with one of the following methods:

```yaml
# networkConfig holds the configuration for the ProjectOrigin network configuration
networkConfig:
  # url defines an url to fetch the network configuration from, allowed formats are json or yaml
  url: # https://example.com/networkConfiguration.json

  # configMap defines an existing configmap to fetch the network configuration from
  configMap:
    # name: verifier-network-configuration
    # key: networkConfiguration.json

  # yaml defines the network configuration as a string in yaml
  yaml: #|-
  #   registries:
  #     narniaReegistry:
  #       url: "https://registry.narnia.example.com",
  #   areas:
  #     DK1:
  #       issuerKeys:
  #       - publicKey: "Ay02vkc6FGV8FwtvVsmBO2p7UdbZIcFhvMGFB40D3DKX"

  # json defines the network configuration as a string in json
  json: #|-
  #  {
  #    "registries": {
  #      "narniaReegistry": {
  #        "url": "https://registry.narnia.example.com"
  #      }
  #    },
  #    "areas": {
  #      "DK1": {
  #        "issuerKeys": [
  #          {
  #            "publicKey": "Ay02vkc6FGV8FwtvVsmBO2p7UdbZIcFhvMGFB40D3DKX"
  #          }
  #        ]
  #      }
  #    }
  #  }
  ```

## Generating a issuer key

An issuer key is the public-private key-pair used by an issuing body
to issue certificates on the registries.

Issuer algorithm used is the ED25519 curve,
this is one of the most used curves for signing and is in broad use
and is tried and tested.

To generate a private key one can use openssl,
below we generate a key for narnia.

```shell
openssl genpkey -algorithm ED25519 -out narnia.pem
```

> NOTE: This is the private key which must be kept secure

### Deriving public key

To derive the public key to be shared with the registry verifiers one
can use openssl, here the key is written to a file named
narnia.pub

```shell
openssl pkey -in narnia.pem -pubout > narnia.pub
```

#### Add it values.yaml file

To add the narnia.pub to the values file,
one must encode the file as base64,
this can again be done using the shell

```shell
cat narnia.pub | base64 -w 0
```

> note: the `-w 0` is to disable word-wrap of the output depending on the platform
