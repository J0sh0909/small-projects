# 2025-09-18 22:16:52 by RouterOS 7.19.6
#
# model = RB750Gr3
/interface bridge
add admin-mac=D0:EA:11:1C:B6:02 auto-mac=no comment=defconf name=bridge \
    vlan-filtering=yes
/interface vlan
add interface=bridge name=vlan10 vlan-id=10
add interface=bridge name=vlan20 vlan-id=20
add interface=bridge name=vlan30 vlan-id=30
add interface=bridge name=vlan40 vlan-id=40
add interface=bridge name=vlan50 vlan-id=50
add interface=bridge name=vlan51 vlan-id=51
add interface=bridge name=vlan52 vlan-id=52
add interface=bridge name=vlan53 vlan-id=53
add interface=bridge name=vlan54 vlan-id=54
add interface=bridge name=vlan55 vlan-id=55
add interface=bridge name=vlan56 vlan-id=56
add interface=bridge name=vlan57 vlan-id=57
add interface=bridge name=vlan58 vlan-id=58
add interface=bridge name=vlan59 vlan-id=59
add interface=bridge name=vlan60 vlan-id=60
add interface=bridge name=vlan70 vlan-id=70
add interface=bridge name=vlan80 vlan-id=80
add interface=bridge name=vlan90 vlan-id=90
add interface=bridge name=vlan100 vlan-id=100
add interface=bridge name=vlan128 vlan-id=128
add interface=bridge name=vlan129 vlan-id=129
add interface=bridge name=vlan200 vlan-id=200
add interface=bridge name=vlan254 vlan-id=254
add interface=bridge name=vlan1000 vlan-id=1000
/interface list
add name=WAN
/ip pool
add name=pool-vlan10 ranges=192.168.10.50-192.168.10.254
add name=pool-vlan50 ranges=192.168.50.50-192.168.50.254
add name=pool-vlan51 ranges=192.168.51.50-192.168.51.254
add name=pool-vlan52 ranges=192.168.52.50-192.168.52.254
add name=pool-vlan53 ranges=192.168.53.50-192.168.53.254
add name=pool-vlan54 ranges=192.168.54.50-192.168.54.254
add name=pool-vlan55 ranges=192.168.55.50-192.168.55.254
add name=pool-vlan56 ranges=192.168.56.50-192.168.56.254
add name=pool-vlan57 ranges=192.168.57.50-192.168.57.254
add name=pool-vlan58 ranges=192.168.58.50-192.168.58.254
add name=pool-vlan59 ranges=192.168.59.50-192.168.59.254
add name=pool-vlan70 ranges=192.168.70.50-192.168.70.254
add name=pool-vlan80 ranges=192.168.80.50-192.168.80.254
add name=pool-vlan90 ranges=192.168.90.50-192.168.90.254
add name=pool-vlan128 ranges=192.168.128.2-192.168.128.254
add name=pool-vlan129 ranges=192.168.129.2-192.168.129.254
add name=pool-vlan200 ranges=192.168.200.50-192.168.200.254
/ip dhcp-server
add address-pool=pool-vlan10 interface=vlan10 lease-time=1d name=dhcp-vlan10
add address-pool=pool-vlan50 interface=vlan50 lease-time=1d name=dhcp-vlan50
add address-pool=pool-vlan51 interface=vlan51 lease-time=1d name=dhcp-vlan51
add address-pool=pool-vlan52 interface=vlan52 lease-time=1d name=dhcp-vlan52
add address-pool=pool-vlan53 interface=vlan53 lease-time=1d name=dhcp-vlan53
add address-pool=pool-vlan54 interface=vlan54 lease-time=1d name=dhcp-vlan54
add address-pool=pool-vlan55 interface=vlan55 lease-time=1d name=dhcp-vlan55
add address-pool=pool-vlan56 interface=vlan56 lease-time=1d name=dhcp-vlan56
add address-pool=pool-vlan57 interface=vlan57 lease-time=1d name=dhcp-vlan57
add address-pool=pool-vlan58 interface=vlan58 lease-time=1d name=dhcp-vlan58
add address-pool=pool-vlan59 interface=vlan59 lease-time=1d name=dhcp-vlan59
add address-pool=pool-vlan70 interface=vlan70 lease-time=1d name=dhcp-vlan70
add address-pool=pool-vlan80 interface=vlan80 lease-time=1d name=dhcp-vlan80
add address-pool=pool-vlan90 interface=vlan90 lease-time=1d name=dhcp-vlan90
add address-pool=pool-vlan128 interface=vlan128 lease-time=1d name=\
    dhcp-vlan128
add address-pool=pool-vlan129 interface=vlan129 lease-time=1d name=\
    dhcp-vlan129
add address-pool=pool-vlan200 interface=vlan200 lease-time=4w2d name=\
    dhcp-vlan200
/interface bridge port
add bridge=bridge comment=defconf interface=ether1
add bridge=bridge comment=defconf interface=ether2 pvid=777
add bridge=bridge comment=defconf frame-types=\
    admit-only-untagged-and-priority-tagged interface=ether3 pvid=254
add bridge=bridge comment=defconf interface=ether4 pvid=777
add bridge=bridge comment=defconf interface=ether5 pvid=254
/interface bridge vlan
add bridge=bridge untagged=bridge vlan-ids=777
add bridge=bridge vlan-ids=666
add bridge=bridge tagged=ether2,ether4,ether5 vlan-ids=10
add bridge=bridge tagged=ether2 vlan-ids=20
add bridge=bridge tagged=ether2 vlan-ids=30
add bridge=bridge tagged=ether2 vlan-ids=40
add bridge=bridge tagged=ether2 vlan-ids=50
add bridge=bridge tagged=ether2 vlan-ids=51
add bridge=bridge tagged=ether2 vlan-ids=52
add bridge=bridge tagged=ether2 vlan-ids=53
add bridge=bridge tagged=ether2 vlan-ids=54
add bridge=bridge tagged=ether2 vlan-ids=55
add bridge=bridge tagged=ether2 vlan-ids=56
add bridge=bridge tagged=ether2 vlan-ids=57
add bridge=bridge tagged=ether2 vlan-ids=58
add bridge=bridge tagged=ether2 vlan-ids=59
add bridge=bridge tagged=ether2 vlan-ids=60
add bridge=bridge tagged=ether2 vlan-ids=70
add bridge=bridge tagged=ether2 vlan-ids=80
add bridge=bridge tagged=ether2 vlan-ids=90
add bridge=bridge tagged=ether2 vlan-ids=100
add bridge=bridge tagged=ether2,ether4,ether5 vlan-ids=128
add bridge=bridge tagged=ether2,ether4,ether5 vlan-ids=129
add bridge=bridge tagged=ether2,ether4,ether5 vlan-ids=200
add bridge=bridge tagged=ether2,ether4 untagged=bridge,ether3,ether5 \
    vlan-ids=254
add bridge=bridge tagged=ether2 vlan-ids=1000
/interface list member
add interface=ether1 list=WAN
/ip address
add address=192.168.10.1/24 interface=vlan10 network=192.168.10.0
add address=192.168.20.1/24 interface=vlan20 network=192.168.20.0
add address=192.168.30.1/24 interface=vlan30 network=192.168.30.0
add address=192.168.40.1/24 interface=vlan40 network=192.168.40.0
add address=192.168.50.1/24 interface=vlan50 network=192.168.50.0
add address=192.168.51.1/24 interface=vlan51 network=192.168.51.0
add address=192.168.52.1/24 interface=vlan52 network=192.168.52.0
add address=192.168.53.1/24 interface=vlan53 network=192.168.53.0
add address=192.168.54.1/24 interface=vlan54 network=192.168.54.0
add address=192.168.55.1/24 interface=vlan55 network=192.168.55.0
add address=192.168.56.1/24 interface=vlan56 network=192.168.56.0
add address=192.168.57.1/24 interface=vlan57 network=192.168.57.0
add address=192.168.58.1/24 interface=vlan58 network=192.168.58.0
add address=192.168.59.1/24 interface=vlan59 network=192.168.59.0
add address=192.168.60.1/24 interface=vlan60 network=192.168.60.0
add address=192.168.70.1/24 interface=vlan70 network=192.168.70.0
add address=192.168.80.1/24 interface=vlan80 network=192.168.80.0
add address=192.168.90.1/24 interface=vlan90 network=192.168.90.0
add address=192.168.100.1/24 interface=vlan100 network=192.168.100.0
add address=192.168.128.1/24 interface=vlan128 network=192.168.128.0
add address=192.168.129.1/24 interface=vlan129 network=192.168.129.0
add address=192.168.200.1/24 interface=vlan200 network=192.168.200.0
add address=192.168.254.1/28 interface=vlan254 network=192.168.254.0
add address=192.168.0.1/24 interface=vlan1000 network=192.168.0.0
/ip dhcp-client
add comment=defconf interface=bridge
# Interface not active
add comment="WAN IPv4" interface=ether1
/ip dhcp-server network
add address=192.168.10.0/24 dns-server=192.168.10.1 gateway=192.168.10.1
add address=192.168.50.0/24 dns-server=192.168.50.1 gateway=192.168.50.1
add address=192.168.51.0/24 dns-server=192.168.51.1 gateway=192.168.51.1
add address=192.168.52.0/24 dns-server=192.168.52.1 gateway=192.168.52.1
add address=192.168.53.0/24 dns-server=192.168.53.1 gateway=192.168.53.1
add address=192.168.54.0/24 dns-server=192.168.54.1 gateway=192.168.54.1
add address=192.168.55.0/24 dns-server=192.168.55.1 gateway=192.168.55.1
add address=192.168.56.0/24 dns-server=192.168.56.1 gateway=192.168.56.1
add address=192.168.57.0/24 dns-server=192.168.57.1 gateway=192.168.57.1
add address=192.168.58.0/24 dns-server=192.168.58.1 gateway=192.168.58.1
add address=192.168.59.0/24 dns-server=192.168.59.1 gateway=192.168.59.1
add address=192.168.70.0/24 dns-server=192.168.70.1 gateway=192.168.70.1
add address=192.168.80.0/24 dns-server=192.168.80.1 gateway=192.168.80.1
add address=192.168.90.0/24 dns-server=192.168.90.1 gateway=192.168.90.1
add address=192.168.128.0/24 dns-server=192.168.128.1 gateway=192.168.128.1
add address=192.168.129.0/24 dns-server=192.168.129.1 gateway=192.168.129.1
add address=192.168.200.0/24 dns-server=192.168.200.1 gateway=192.168.200.1
/ip dns
set allow-remote-requests=yes servers=\
    1.1.1.1,8.8.8.8,2606:4700:4700::1111,2001:4860:4860::8888
/ip dns static
add address=192.168.254.2 name=crs305.mgmt type=A
add address=192.168.254.1 name=rb750.mgmt type=A
add address=192.168.254.3 name=oc220.mgmt type=A
add address=192.168.254.4 name=eap1.mgmt type=A
add address=192.168.254.5 name=eap2.mgmt type=A
add address=192.168.254.10 name=ai.mgmt type=A
/ip firewall address-list
add address=192.168.10.0/24 list=trusted
add address=192.168.50.0/24 list=trusted
add address=192.168.51.0/24 list=trusted
add address=192.168.52.0/24 list=trusted
add address=192.168.53.0/24 list=trusted
add address=192.168.54.0/24 list=trusted
add address=192.168.55.0/24 list=trusted
add address=192.168.56.0/24 list=trusted
add address=192.168.57.0/24 list=trusted
add address=192.168.58.0/24 list=trusted
add address=192.168.59.0/24 list=trusted
add address=192.168.70.0/24 list=trusted
add address=192.168.80.0/24 list=trusted
add address=192.168.20.0/24 list=infra
add address=192.168.30.0/24 list=infra
add address=192.168.40.0/24 list=infra
add address=192.168.60.0/24 list=infra
add address=192.168.0.0/24 list=hypervisor
add address=192.168.100.0/24 list=dmz
add address=192.168.90.0/24 list=sandbox
add address=192.168.128.0/24 list=untrusted
add address=192.168.129.0/24 list=untrusted
add address=192.168.200.0/24 list=untrusted
add address=192.168.254.0/24 list=mgmt
add address=10.0.0.0/8 list=rfc1918
add address=172.16.0.0/12 list=rfc1918
add address=192.168.0.0/16 list=rfc1918
add address=192.168.10.0/24 comment=Private list=dns-clients
add address=192.168.50.0/24 comment=VM1 list=dns-clients
add address=192.168.51.0/24 comment=VM2 list=dns-clients
add address=192.168.52.0/24 comment=VM3 list=dns-clients
add address=192.168.53.0/24 comment=VM4 list=dns-clients
add address=192.168.54.0/24 comment=VM5 list=dns-clients
add address=192.168.55.0/24 comment=VM6 list=dns-clients
add address=192.168.56.0/24 comment=VM7 list=dns-clients
add address=192.168.57.0/24 comment=VM8 list=dns-clients
add address=192.168.58.0/24 comment=VM9 list=dns-clients
add address=192.168.59.0/24 comment=VM10 list=dns-clients
add address=192.168.70.0/24 comment=Apps list=dns-clients
add address=192.168.80.0/24 comment=Dev list=dns-clients
add address=192.168.128.0/24 comment=Upstairs list=dns-clients
add address=192.168.129.0/24 comment=Basement list=dns-clients
add address=192.168.200.0/24 comment=IoT list=dns-clients
/ip firewall filter
add action=accept chain=input comment="Input: Accept established/related" \
    connection-state=established,related
add action=drop chain=input comment="Input: Drop invalid" connection-state=\
    invalid
add action=accept chain=input comment="Input: MGMT access to router" \
    src-address-list=mgmt
add action=accept chain=input comment="Input: Hypervisor access to router" \
    src-address-list=hypervisor
add action=accept chain=input comment="Input: ICMP from Trusted" protocol=\
    icmp src-address-list=trusted
add action=accept chain=input comment="Input: ICMP from Hypervisor" protocol=\
    icmp src-address-list=hypervisor
add action=accept chain=input comment="Allow DNS UDP from DHCP VLANs" \
    dst-port=53 protocol=udp src-address-list=dns-clients
add action=accept chain=input comment="Allow DNS TCP from DHCP VLANs" \
    dst-port=53 protocol=tcp src-address-list=dns-clients
add action=drop chain=input comment="Input: Default deny"
add action=fasttrack-connection chain=forward comment=FastTrack \
    connection-state=established,related hw-offload=yes
add action=accept chain=forward comment="Accept established/related" \
    connection-state=established,related
add action=drop chain=forward comment="Drop invalid" connection-state=invalid
add action=accept chain=forward comment="Trusted to Trusted" \
    dst-address-list=trusted src-address-list=trusted
add action=accept chain=forward comment="Hypervisor to MGMT" \
    dst-address-list=mgmt src-address-list=hypervisor
add action=accept chain=forward comment="MGMT to MGMT" dst-address-list=mgmt \
    src-address-list=mgmt
add action=accept chain=forward comment="Trusted to DMZ" dst-address-list=dmz \
    src-address-list=trusted
add action=accept chain=forward comment="Hypervisor to DMZ" dst-address-list=\
    dmz src-address-list=hypervisor
add action=accept chain=forward comment="Hypervisor to Infra" \
    dst-address-list=infra src-address-list=hypervisor
add action=accept chain=forward comment="Hypervisor to Trusted" \
    dst-address-list=trusted src-address-list=hypervisor
add action=accept chain=forward comment="Infra to Infra" dst-address-list=\
    infra src-address-list=infra
add action=accept chain=forward comment="Apps to Infra" dst-address-list=\
    infra src-address=192.168.70.0/24
add action=accept chain=forward comment="Dev to Infra" dst-address-list=infra \
    src-address=192.168.80.0/24
add action=accept chain=forward comment="VM50-51 to Infra" dst-address-list=\
    infra src-address=192.168.50.0/23
add action=accept chain=forward comment="VM52-53 to Infra" dst-address-list=\
    infra src-address=192.168.52.0/23
add action=accept chain=forward comment="VM54-55 to Infra" dst-address-list=\
    infra src-address=192.168.54.0/23
add action=accept chain=forward comment="VM56-57 to Infra" dst-address-list=\
    infra src-address=192.168.56.0/23
add action=accept chain=forward comment="VM58-59 to Infra" dst-address-list=\
    infra src-address=192.168.58.0/23
add action=accept chain=forward comment="Private(10) to IoT(200)" \
    dst-address=192.168.200.0/24 src-address=192.168.10.0/24
add action=accept chain=forward comment="Hypervisor(1000) to IoT(200)" \
    dst-address=192.168.200.0/24 src-address=192.168.0.0/24
add action=accept chain=forward comment="Client(128) to IoT(200)" \
    dst-address=192.168.200.0/24 src-address=192.168.128.0/24
add action=accept chain=forward comment="IoT(200) to Client(128)" \
    dst-address=192.168.128.0/24 src-address=192.168.200.0/24
add action=accept chain=forward comment="Client(129) to IoT(200)" \
    dst-address=192.168.200.0/24 src-address=192.168.129.0/24
add action=accept chain=forward comment="IoT(200) to Client(129)" \
    dst-address=192.168.129.0/24 src-address=192.168.200.0/24
add action=accept chain=forward comment="Sandbox internal" dst-address-list=\
    sandbox src-address-list=sandbox
add action=accept chain=forward comment="Trusted to WAN" out-interface-list=\
    WAN src-address-list=trusted
add action=accept chain=forward comment="Hypervisor to WAN" \
    out-interface-list=WAN src-address-list=hypervisor
add action=accept chain=forward comment="Untrusted to WAN" \
    out-interface-list=WAN src-address-list=untrusted
add action=accept chain=forward comment="DMZ to WAN" out-interface-list=WAN \
    src-address-list=dmz
add action=drop chain=forward comment="DMZ cannot initiate to internal" \
    src-address-list=dmz
add action=drop chain=forward comment="Sandbox cannot reach outside" \
    src-address-list=sandbox
add action=drop chain=forward comment="Nothing reaches Sandbox" \
    dst-address-list=sandbox
add action=drop chain=forward comment="Untrusted blocked from RFC1918" \
    dst-address-list=rfc1918 src-address-list=untrusted
add action=drop chain=forward comment="Block all to MGMT" dst-address-list=\
    mgmt
add action=drop chain=forward comment="Default deny all"
/ip firewall nat
# in/out-interface matcher not possible when interface (ether1) is slave - use master instead (bridge)
add action=masquerade chain=srcnat comment="NAT to WAN" out-interface=ether1
# in/out-interface matcher not possible when interface (ether1) is slave - use master instead (bridge)
add action=masquerade chain=srcnat comment="NAT to WAN" out-interface=ether1
/ipv6 address
add address=fd5d:c575:3bea:10::1 interface=vlan10
add address=fd5d:c575:3bea:20::1 interface=vlan20
add address=fd5d:c575:3bea:30::1 interface=vlan30
add address=fd5d:c575:3bea:40::1 interface=vlan40
add address=fd5d:c575:3bea:50::1 interface=vlan50
add address=fd5d:c575:3bea:51::1 interface=vlan51
add address=fd5d:c575:3bea:52::1 interface=vlan52
add address=fd5d:c575:3bea:53::1 interface=vlan53
add address=fd5d:c575:3bea:54::1 interface=vlan54
add address=fd5d:c575:3bea:55::1 interface=vlan55
add address=fd5d:c575:3bea:56::1 interface=vlan56
add address=fd5d:c575:3bea:57::1 interface=vlan57
add address=fd5d:c575:3bea:58::1 interface=vlan58
add address=fd5d:c575:3bea:59::1 interface=vlan59
add address=fd5d:c575:3bea:60::1 interface=vlan60
add address=fd5d:c575:3bea:70::1 interface=vlan70
add address=fd5d:c575:3bea:80::1 interface=vlan80
add address=fd5d:c575:3bea:90::1 interface=vlan90
add address=fd5d:c575:3bea:100::1 interface=vlan100
add address=fd5d:c575:3bea:128::1 interface=vlan128
add address=fd5d:c575:3bea:129::1 interface=vlan129
add address=fd5d:c575:3bea:200::1 interface=vlan200
add address=fd5d:c575:3bea:254::1 interface=vlan254
add address=fd5d:c575:3bea::1 interface=vlan1000
/ipv6 dhcp-client
add add-default-route=yes comment="WAN IPv6 PD" interface=ether1 pool-name=\
    isp-pool request=prefix
/ipv6 firewall address-list
add address=fd5d:c575:3bea:10::/64 list=trusted6
add address=fd5d:c575:3bea:50::/64 list=trusted6
add address=fd5d:c575:3bea:51::/64 list=trusted6
add address=fd5d:c575:3bea:52::/64 list=trusted6
add address=fd5d:c575:3bea:53::/64 list=trusted6
add address=fd5d:c575:3bea:54::/64 list=trusted6
add address=fd5d:c575:3bea:55::/64 list=trusted6
add address=fd5d:c575:3bea:56::/64 list=trusted6
add address=fd5d:c575:3bea:57::/64 list=trusted6
add address=fd5d:c575:3bea:58::/64 list=trusted6
add address=fd5d:c575:3bea:59::/64 list=trusted6
add address=fd5d:c575:3bea:70::/64 list=trusted6
add address=fd5d:c575:3bea:80::/64 list=trusted6
add address=fd5d:c575:3bea:20::/64 list=infra6
add address=fd5d:c575:3bea:30::/64 list=infra6
add address=fd5d:c575:3bea:40::/64 list=infra6
add address=fd5d:c575:3bea:60::/64 list=infra6
add address=fd5d:c575:3bea::/64 list=hypervisor6
add address=fd5d:c575:3bea:100::/64 list=dmz6
add address=fd5d:c575:3bea:90::/64 list=sandbox6
add address=fd5d:c575:3bea:128::/64 list=untrusted6
add address=fd5d:c575:3bea:129::/64 list=untrusted6
add address=fd5d:c575:3bea:200::/64 list=untrusted6
add address=fd5d:c575:3bea:254::/64 list=mgmt6
add address=fd5d:c575:3bea::/48 list=ula48
add address=fd5d:c575:3bea:10::/64 list=dns-clients6
add address=fd5d:c575:3bea:50::/64 list=dns-clients6
add address=fd5d:c575:3bea:51::/64 list=dns-clients6
add address=fd5d:c575:3bea:52::/64 list=dns-clients6
add address=fd5d:c575:3bea:53::/64 list=dns-clients6
add address=fd5d:c575:3bea:54::/64 list=dns-clients6
add address=fd5d:c575:3bea:55::/64 list=dns-clients6
add address=fd5d:c575:3bea:56::/64 list=dns-clients6
add address=fd5d:c575:3bea:57::/64 list=dns-clients6
add address=fd5d:c575:3bea:58::/64 list=dns-clients6
add address=fd5d:c575:3bea:59::/64 list=dns-clients6
add address=fd5d:c575:3bea:70::/64 list=dns-clients6
add address=fd5d:c575:3bea:80::/64 list=dns-clients6
add address=fd5d:c575:3bea:128::/64 list=dns-clients6
add address=fd5d:c575:3bea:129::/64 list=dns-clients6
add address=fd5d:c575:3bea:200::/64 list=dns-clients6
/ipv6 firewall filter
add action=accept chain=input comment="Input: Accept ICMPv6" protocol=icmpv6
add action=accept chain=input comment="Input: Accept established/related" \
    connection-state=established,related
add action=drop chain=input comment="Input: Drop invalid" connection-state=\
    invalid
add action=accept chain=input comment="Input: MGMT access to router" \
    src-address-list=mgmt6
add action=accept chain=input comment="Input: Hypervisor access to router" \
    src-address-list=hypervisor6
add action=accept chain=input comment="Allow DNS UDP from DHCP VLANs" \
    dst-port=53 protocol=udp src-address-list=dns-clients6
add action=accept chain=input comment="Allow DNS TCP from DHCP VLANs" \
    dst-port=53 protocol=tcp src-address-list=dns-clients6
add action=drop chain=input comment="Input: Default deny"
add action=accept chain=forward comment="Accept all ICMPv6" protocol=icmpv6
add action=accept chain=forward comment="Accept established/related" \
    connection-state=established,related
add action=drop chain=forward comment="Drop invalid" connection-state=invalid
add action=accept chain=forward comment="Trusted to Trusted" \
    dst-address-list=trusted6 src-address-list=trusted6
add action=accept chain=forward comment="Hypervisor to MGMT" \
    dst-address-list=mgmt6 src-address-list=hypervisor6
add action=accept chain=forward comment="MGMT to MGMT" dst-address-list=mgmt6 \
    src-address-list=mgmt6
add action=accept chain=forward comment="Trusted to DMZ" dst-address-list=\
    dmz6 src-address-list=trusted6
add action=accept chain=forward comment="Hypervisor to DMZ" dst-address-list=\
    dmz6 src-address-list=hypervisor6
add action=accept chain=forward comment="Hypervisor to Infra" \
    dst-address-list=infra6 src-address-list=hypervisor6
add action=accept chain=forward comment="Hypervisor to Trusted" \
    dst-address-list=trusted6 src-address-list=hypervisor6
add action=accept chain=forward comment="Infra to Infra" dst-address-list=\
    infra6 src-address-list=infra6
add action=accept chain=forward comment="Apps to Infra" dst-address-list=\
    infra6 src-address=fd5d:c575:3bea:70::/64
add action=accept chain=forward comment="Dev to Infra" dst-address-list=\
    infra6 src-address=fd5d:c575:3bea:80::/64
add action=accept chain=forward comment="VM50 to Infra" dst-address-list=\
    infra6 src-address=fd5d:c575:3bea:50::/64
add action=accept chain=forward comment="VM51 to Infra" dst-address-list=\
    infra6 src-address=fd5d:c575:3bea:51::/64
add action=accept chain=forward comment="VM52 to Infra" dst-address-list=\
    infra6 src-address=fd5d:c575:3bea:52::/64
add action=accept chain=forward comment="VM53 to Infra" dst-address-list=\
    infra6 src-address=fd5d:c575:3bea:53::/64
add action=accept chain=forward comment="VM54 to Infra" dst-address-list=\
    infra6 src-address=fd5d:c575:3bea:54::/64
add action=accept chain=forward comment="VM55 to Infra" dst-address-list=\
    infra6 src-address=fd5d:c575:3bea:55::/64
add action=accept chain=forward comment="VM56 to Infra" dst-address-list=\
    infra6 src-address=fd5d:c575:3bea:56::/64
add action=accept chain=forward comment="VM57 to Infra" dst-address-list=\
    infra6 src-address=fd5d:c575:3bea:57::/64
add action=accept chain=forward comment="VM58 to Infra" dst-address-list=\
    infra6 src-address=fd5d:c575:3bea:58::/64
add action=accept chain=forward comment="VM59 to Infra" dst-address-list=\
    infra6 src-address=fd5d:c575:3bea:59::/64
add action=accept chain=forward comment="Private(10) to IoT(200)" \
    dst-address=fd5d:c575:3bea:200::/64 src-address=fd5d:c575:3bea:10::/64
add action=accept chain=forward comment="Hypervisor(1000) to IoT(200)" \
    dst-address=fd5d:c575:3bea:200::/64 src-address=fd5d:c575:3bea::/64
add action=accept chain=forward comment="Client(128) to IoT(200)" \
    dst-address=fd5d:c575:3bea:200::/64 src-address=fd5d:c575:3bea:128::/64
add action=accept chain=forward comment="IoT(200) to Client(128)" \
    dst-address=fd5d:c575:3bea:128::/64 src-address=fd5d:c575:3bea:200::/64
add action=accept chain=forward comment="Client(129) to IoT(200)" \
    dst-address=fd5d:c575:3bea:200::/64 src-address=fd5d:c575:3bea:129::/64
add action=accept chain=forward comment="IoT(200) to Client(129)" \
    dst-address=fd5d:c575:3bea:129::/64 src-address=fd5d:c575:3bea:200::/64
add action=accept chain=forward comment="Sandbox internal" dst-address-list=\
    sandbox6 src-address-list=sandbox6
add action=drop chain=forward comment="Trusted6 block ULA" dst-address-list=\
    ula48 src-address-list=trusted6
add action=accept chain=forward comment="Trusted to WAN" src-address-list=\
    trusted6
add action=drop chain=forward comment="Hypervisor6 block ULA" \
    dst-address-list=ula48 src-address-list=hypervisor6
add action=accept chain=forward comment="Hypervisor to WAN" src-address-list=\
    hypervisor6
add action=drop chain=forward comment="Untrusted6 block ULA" \
    dst-address-list=ula48 src-address-list=untrusted6
add action=accept chain=forward comment="Untrusted to WAN" src-address-list=\
    untrusted6
add action=drop chain=forward comment="DMZ6 block ULA" dst-address-list=ula48 \
    src-address-list=dmz6
add action=accept chain=forward comment="DMZ to WAN" src-address-list=dmz6
add action=drop chain=forward comment="DMZ cannot initiate to internal" \
    src-address-list=dmz6
add action=drop chain=forward comment="Sandbox cannot reach outside" \
    src-address-list=sandbox6
add action=drop chain=forward comment="Nothing reaches Sandbox" \
    dst-address-list=sandbox6
add action=drop chain=forward comment="Untrusted blocked from ULA space" \
    dst-address-list=ula48 src-address-list=untrusted6
add action=drop chain=forward comment="Block all to MGMT" dst-address-list=\
    mgmt6
add action=drop chain=forward comment="Default deny all"
/system identity
set name=RB750Gr3
