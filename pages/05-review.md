---
layout: page
title: "Step 5 : Review"
permalink: /example-use-case/05-review
---


# Review of created secrets
Once AzureKeyVault is applied, following kubernetes secrets are expected to be created in respective namespace(s).

<div style="margin:30px 0px 0px 0px;"><img class="ex-img" src="../assets/img/examples/superstore-dev-secrets.png" alt="Secrets in superstore-dev namespace" /></div>

Only **docker-registry-credential** secret was created due to **3rd** managed secret definition.

<div style="margin:40px 0px 0px 0px;"><img class="ex-img" src="../assets/img/examples/superstore-test-secrets.png" alt="Secrets in superstore-test namespace" /></div>
All managed secret definitions are matched with superstore-test namespace. Therefore this namespace includes all three secrets.


<div style="margin:40px 0px 0px 0px;"><img class="ex-img" src="../assets/img/examples/newsletter-dev-secrets.png" alt="Secrets in newsletter-dev namespace" /></div>

Only **docker-registry-credential** secret was created due to **3rd** managed secret definition.

<div style="margin:40px 0px 0px 0px;"><img class="ex-img" src="../assets/img/examples/newsletter-test-secrets.png" alt="Secrets in newsletter-test namespace" /></div>

**2nd** and **3rd** definition is matched with newsletter-test namespace.

## Before & After of jsonpath declaration
Let's remember how **amqp** was declared in `.spec.managedSecrets[]` and how it is crated as kubernetes cluster;

```yaml
...
- name: amqp
    namespaces:
    - "(.+)-test"
    type: Opaque
    data:
    amqp-connectionstring: "amqp://\$['amqp-username']:\$['amqp-password']@brokersvc:5672/"
...
```

> In yaml definition, because of the bash syntax, we have to use <b>\ </b> character to escape $ sign.

<div style="margin:20px 0px 20px 0px;"><img class="ex-img" src="../assets/img/examples/amqp-managedsecret.png" alt="Secrets in newsletter-test namespace" /></div>


This is how created kubernetes secret looks like;

<div style="margin:0px 0px 0px 0px;"><img class="ex-img" src="../assets/img/examples/amqp-kubernetes-secret.png" alt="Secrets in newsletter-test namespace" /></div>

<div class="ex-nav">
  <div class="left-nav">
    <a href="{{ '/example-use-case/04-creating-azurekeyvault-custom-object' | prepend: site.baseurl }}">
      << Step 4 : Creating AzureKeyVault Custom Object
    </a>
  </div>
  <div class="right-nav">
  </div>
</div>
<br>