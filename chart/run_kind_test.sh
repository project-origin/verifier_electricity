#!/bin/bash

# This script is used to test the chronicler chart using kind.
# It installs the chart and validates it starts up correctly.

# Define kind cluster name
cluster_name=verifier-test
namespace=verifier

# Ensures script fails if something goes wrong.
set -eo pipefail

# define cleanup function
cleanup() {
    rm -fr $temp_folderx
    kind delete cluster --name ${cluster_name} >/dev/null 2>&1
}

# define debug function
debug() {
    echo -e "\nDebugging information:"
    echo -e "\nHelm status:"
    helm status verifier --namespace ${namespace} --show-desc --show-resources

    echo -e "\nDeployment description:"
    kubectl describe deployment --namespace ${namespace} po-electricity-deployment

    POD_NAMES=$(kubectl get pods --namespace ${namespace} -l app=po-electricity -o jsonpath="{.items[*].metadata.name}")
    # Loop over the pods and print their logs
    for POD_NAME in $POD_NAMES
    do
        echo -e "\nLogs for $POD_NAME:"
        kubectl logs --namespace ${namespace} $POD_NAME
    done
}

# trap cleanup function on script exit
trap 'cleanup' 0
trap 'debug; cleanup' ERR

# define variables
temp_folder=$(mktemp -d)
values_filename=${temp_folder}/values.yaml

# create kind cluster
kind delete cluster --name ${cluster_name}
kind create cluster --name ${cluster_name}

# create namespace
kubectl create namespace ${namespace}

# build docker image
make build-container

# load docker image into cluster
kind load --name ${cluster_name} docker-image ghcr.io/project-origin/electricity-server:test

# generate keys
PrivateKey=$(openssl genpkey -algorithm ED25519)
PrivateKeyBase64=$(echo "$PrivateKey" | base64 -w 0)
PublicKeyBase64=$(echo "$PrivateKey" | openssl pkey -pubout | base64 -w 0)

# generate values.yaml file
cat << EOF > "${values_filename}"
image:
  tag: test
replicaCount: 1
networkConfig:
  yaml: |-
    registries:
      example-registry:
        url: http://example-registry:5000
    areas:
      Narnia:
        issuerKeys:
          - publicKey: $PublicKeyBase64

EOF

# install chronicler chart
helm install electricity ./chart --values ${values_filename} --namespace ${namespace} --wait --timeout 1m

# verify deployment is ready
deployments_status=$(kubectl get deployments --namespace ${namespace} --no-headers | awk '$3 != $4 {print "Deployment " $1 " is not ready"}')

# Print the results to stderr if there are any issues
if [ -n "$deployments_status" ]; then
    echo "$deployments_status" 1>&2
    echo "Test failed ❌"
else
    echo "Test completed successfully ✅"
fi
