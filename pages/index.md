---
layout: page
title: Azure KeyVault Secret Operator for Kubernetes
permalink: /
---

[![Artifact Hub](https://img.shields.io/endpoint?url=https://artifacthub.io/badge/repository/azure-keyvault-secret-operator)](https://artifacthub.io/packages/search?repo=azure-keyvault-secret-operator)
[![Release](https://img.shields.io/github/v/release/btungut/azure-keyvault-secret-operator?include_prereleases&style=plastic)](https://github.com/btungut/azure-keyvault-secret-operator/releases)
[![LICENSE](https://img.shields.io/github/license/btungut/azure-keyvault-secret-operator?style=plastic)](https://github.com/btungut/azure-keyvault-secret-operator/blob/master/LICENSE)

# Azure KeyVault Secret Operator for Kubernetes

Easy to use operator which is able to **sync all of the Azure KeyVault secrets** into your Kubernetes cluster with **only one manifest**. 

Operator can run on non Azure environments without any kind of other prerequisites like CSI driver, ARC enabling, etc.
All you need to have a **Service Principal** which is used to access **Azure KeyVault**.

<div style="text-align:center;">
<img style="width: 1368px;" src="assets/img/home/AzureKeyVault-object.png" alt="AzureKeyVault object and created secrets" />
<p style="text-align:center; margin-top:20px;">Example AzureKeyVault custom object and created kubernetes secrets by operator</p>
</div>

### No need to use CSI driver
Azure KeyVault Secret Operator doesn't need any kind of ~~Container Storage Interface~~.

### Only one object for all secrets
Once operator is installed, only one `AzureKeyVault` custom object is sufficient to sync all of the secrets from an Azure KeyVault to **multiple namespaces**

### Regex support
 `AzureKeyVault` object supports regex on namespaces. Therefore you can create a kubernetes secret accross multiple namespaces.

 ```yaml
...
  - name: catalog-api-credentials
      namespaces:
        - "hard-coded-ns"
        - "^((?!kube).)*$"
      data: ...
```

### Jsonpath support
You can put more than one Azure KeyVault secret value into single value of a kubernetes secret.

```yaml
...
  - name: catalog-api-credentials
      namespaces: ...
      data:
        mssql: "$['nameOfAzureKeyVaultSecret']"
        amqp: "$['amqp-host'];port=$['amqp-port'];TLS=enabled" #multiple KeyVault secret into one field
        hardcodedfield: "hard coded value"
```


<div class="section-index">
    <hr class="panel-line">
    <div class="entry">
      <h5><a href="quick-guide">Quick Guide</a></h5>
      <p>Quick way to recognize core features of operator.</p>
    </div>
    <div class="entry">
      <h5><a href="example-use-case">Example Use Case</a></h5>
      <p>You should take a look there, if you'd like to learn all of the features with a real-world, end-to-end scenario.</p>
    </div>
    
</div>