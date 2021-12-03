---
layout: page
title: "Step 4 : Creating AzureKeyVault Custom Object"
permalink: /example-use-case/04-creating-azurekeyvault-custom-object
---

# Creating AzureKeyVault Custom Object
Please visit and review the <a href="{{ '/example-use-case#secrets-need-to-be-created' | prepend: site.baseurl }}">secrets need to be created</a> by operator again.

An AzureKeyVault custom object represents **only one** Azure KeyValt resource and it can manage **multiple secret** in kubernetes. Therefore, it is sufficient to create only one AzureKeyVault custom object that includes three secret definition. 

Following bash command fills required field with declared variables and creates the AzureKeyVault custom object.

> If variables haven't been declared as needed, please copy the manifest from below and fill all required fields. You can easily identify required fields by searching **$** sign. Or, you can download already filled manifest <a href="{{ '/manifests/AzureKeyVault.yaml' | prepend: site.baseurl }}">from here</a>.


```yaml
cat <<EOF | kubectl apply -f -
apiVersion: btungut.io/v1
kind: AzureKeyVault
metadata:
  name: superstore
spec:
  syncVersion: 1
  azureKeyVaultRef:
    name: "$KV_NAME"
    resourceGroup: "$KV_RG"
  servicePrincipalRef:
    secretName: "$SECRET_NAME"
    secretNamespace: "$SECRET_NS"
    tenantIdField: "tenantid"
    clientIdField: "clientid"
    clientSecretField: "clientsecret"
  managedSecrets:

    - name: catalog-api
      namespaces:
        - "superstore-test"
      type: Opaque
      data:
        catalogdb-connectionstring: "\$['catalogdb']"

    - name: amqp
      namespaces:
        - "(.+)-test"
      type: Opaque
      data:
        amqp-connectionstring: "amqp://\$['amqp-username']:\$['amqp-password']@brokersvc:5672/"

    - name: docker-registry-credential
      namespaces:
        - "^((?!kube).)*$"
      type: kubernetes.io/dockerconfigjson
      data:
        .dockerconfigjson: "\$['docker-config-json']"
EOF
```


<div class="ex-nav">
  <div class="left-nav">
    <a href="{{ '/example-use-case/03-deploying-operator' | prepend: site.baseurl }}">
      << Step 3 : Deploying Operator
    </a>
  </div>
  <div class="right-nav">
    <a href="{{ '/example-use-case/05-review' | prepend: site.baseurl }}">
      Step 5 : Review >>
    </a>
  </div>
</div>
<br>