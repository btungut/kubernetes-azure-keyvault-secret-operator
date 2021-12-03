---
layout: page
title: Quick Guide
permalink: /quick-guide
---

# Operator Quick Guide
All you need to have a **Service Principal** which is used to access **Azure KeyVault**.
Operator can run on non Azure environments without any kind of other prerequisites like CSI driver, ARC enabling, etc.

## Deployment
Operator may be installed into cluster with helm chart or directly manifests.
### Deploy with Helm Chart
Helm chart supports RBAC and CRD manifests installation. You can pass `--set rbac.enabled=false` if you don't need.

```bash
helm repo add btungut https://btungut.github.io
helm upgrade -i {RELEASE-NAME} btungut/azure-keyvault-secret-operator
```


### Deploy with manifests 
Please visit the [manifests](https://github.com/btungut/azure-keyvault-secret-operator/tree/master/manifests) folder as you can see there are three manifests.

```bash
kubectl apply -f https://raw.githubusercontent.com/btungut/azure-keyvault-secret-operator/master/manifests/01-crd.yaml
kubectl apply -f https://raw.githubusercontent.com/btungut/azure-keyvault-secret-operator/master/manifests/02-rbac.yaml
kubectl apply -f https://raw.githubusercontent.com/btungut/azure-keyvault-secret-operator/master/manifests/03-deployment.yaml
```


## What is AzureKeyVault custom object ?
**AzureKeyVault** is a cluster scoped custom object which is being tracked by operator.
Only prerequisite you need to complete is having a kubernetes secret which includes Service Principal (id, secret, tenantid) in any namespace.

In a AzureKeyVault object, you need to define followings;

| 1st Level Field             | Description                                                                                                                                |
|:----------------------------|:-------------------------------------------------------------------------------------------------------------------------------------------|
| `.spec.syncVersion`         | Version value for providing consistency. You can increment manually if you'd like to sync all of the secrets again. (Optional, default : 1)|
| `.spec.azureKeyVaultRef`    | Reference of Azure KeyVault which includes secret objects                                                                                  |
| `.spec.servicePrincipalRef` | Reference of a kubernetes secret object which includes fields of authorized Service Principal                                              |
| `.spec.managedSecrets`      | List of kubernetes secrets which is being created and filled by operator as your needs.                                                    |


### Namespaces field supports regex
You can define the secrets which you'd like to be created in `.spec.managedSecrets`. If you'd like to create a secret accross more than one namespaces, you can use regex pattern in `.spec.managedSecrets[].namespaces[]'` field as your needs.

### Data field supports Json Path
Also, `.spec.managedSecrets[].data` field supports **json path** for values. You can put more than one Azure KeyVault Secret data in a field.

Below example demonstrates both of the features.

```yaml
apiVersion: btungut.io/v1
kind: AzureKeyVault
metadata:
  name: contoso
spec:
  syncVersion: 1
  azureKeyVaultRef:
    name: my-azure-keyvault-name
    resourceGroup: my-azure-resourcegroup
  servicePrincipalRef:
    secretName: "my-secret-includes-serviceprincipal"
    secretNamespace: "my-infra-namespace"
    tenantIdField: "tenantid"
    clientIdField: "clientid"
    clientSecretField: "clientsecret"
  managedSecrets:

    - name: catalog-api-credentials
      namespaces:
        - "dev-(.+)"                #namespaces which starts with 'dev-'
        - "hardcodednamespace"      #specific namespace
      type: Opaque
      data:
        mssql: "$['nameOfAzureKeyVaultSecret']"
        amqp: "$['amqp'];port=15672;TLS=enabled"
        hardcodedfield: "hard coded value"
      labels:
        somelabelkey: "it is possible to adding labels"

    - name: docker-pull-secret
      namespaces:
        - "(.+)"                    #match all namespaces
      type: kubernetes.io/dockerconfigjson
      data:
        .dockerconfigjson: "$['acr-credentials-json']"
```


As we mentioned before, you need to have a service principal that is used to access Azure KeyVault. You need to create a secret **manually** which includes service principal informations. You're completely free for naming of the fields. 

Assuming you have following secret in **my-infra-namespace** namespace;
```yaml
apiVersion: v1
kind: Secret
metadata:
  name: my-secret-includes-serviceprincipal
  namespace: my-infra-namespace
type: Opaque
data:
  clientid: //BASE64 encoded GUID
  clientsecret: //BASE64 encoded password
  tenantid: //BASE64 encoded GUID
```
E.g. : Value of `spec.servicePrincipalRef.clientSecretField` in AzureKeyVault object is pointing the data field in service principal secret.
