/*
 * PROJECT:     ReactOS Universal Serial Bus Bulk Enhanced Host Controller Interface
 * LICENSE:     GPL - See COPYING in the top level directory
 * FILE:        drivers/usb/usbccgp/misc.c
 * PURPOSE:     USB  device driver.
 * PROGRAMMERS:
 *              Michael Martin (michael.martin@reactos.org)
 *              Johannes Anderwald (johannes.anderwald@reactos.org)
 *              Cameron Gutman
 */

#include "usbccgp.h"

/* Driver verifier */
IO_COMPLETION_ROUTINE SyncForwardIrpCompletionRoutine;

NTSTATUS
NTAPI
USBSTOR_SyncForwardIrpCompletionRoutine(
    PDEVICE_OBJECT DeviceObject,
    PIRP Irp,
    PVOID Context)
{
    if (Irp->PendingReturned)
    {
        KeSetEvent((PKEVENT)Context, IO_NO_INCREMENT, FALSE);
    }
    return STATUS_MORE_PROCESSING_REQUIRED;
}

NTSTATUS
NTAPI
USBCCGP_SyncForwardIrp(
    PDEVICE_OBJECT DeviceObject,
    PIRP Irp)
{
    KEVENT Event;
    NTSTATUS Status;

    /* Initialize event */
    KeInitializeEvent(&Event, NotificationEvent, FALSE);

    /* Copy irp stack location */
    IoCopyCurrentIrpStackLocationToNext(Irp);

    /* Set completion routine */
    IoSetCompletionRoutine(Irp,
                           USBSTOR_SyncForwardIrpCompletionRoutine,
                           &Event,
                           TRUE,
                           TRUE,
                           TRUE);

    /* Call driver */
    Status = IoCallDriver(DeviceObject, Irp);

    /* Check if pending */
    if (Status == STATUS_PENDING)
    {
        /* Wait for the request to finish */
        KeWaitForSingleObject(&Event, Executive, KernelMode, FALSE, NULL);

        /* Copy status code */
        Status = Irp->IoStatus.Status;
    }

    /* Done */
    return Status;
}

NTSTATUS
USBCCGP_SyncUrbRequest(
    IN PDEVICE_OBJECT DeviceObject,
    OUT PURB UrbRequest)
{
    PIRP Irp;
    PIO_STACK_LOCATION IoStack;
    KEVENT Event;
    NTSTATUS Status;

    /* Allocate irp */
    Irp = IoAllocateIrp(DeviceObject->StackSize, FALSE);
    if (!Irp)
    {
        /* No memory */
        return STATUS_INSUFFICIENT_RESOURCES;
    }

    /* Initialize event */
    KeInitializeEvent(&Event, NotificationEvent, FALSE);

    /* Get next stack location */
    IoStack = IoGetNextIrpStackLocation(Irp);

    /* Initialize stack location */
    IoStack->MajorFunction = IRP_MJ_INTERNAL_DEVICE_CONTROL;
    IoStack->Parameters.DeviceIoControl.IoControlCode = IOCTL_INTERNAL_USB_SUBMIT_URB;
    IoStack->Parameters.Others.Argument1 = (PVOID)UrbRequest;
    IoStack->Parameters.DeviceIoControl.InputBufferLength = UrbRequest->UrbHeader.Length;
    Irp->IoStatus.Status = STATUS_SUCCESS;

    /* Setup completion routine */
    IoSetCompletionRoutine(Irp,
                           USBSTOR_SyncForwardIrpCompletionRoutine,
                           &Event,
                           TRUE,
                           TRUE,
                           TRUE);

    /* Call driver */
    Status = IoCallDriver(DeviceObject, Irp);

    /* Check if request is pending */
    if (Status == STATUS_PENDING)
    {
        /* Wait for completion */
        KeWaitForSingleObject(&Event, Executive, KernelMode, FALSE, NULL);

        /* Update status */
        Status = Irp->IoStatus.Status;
    }

    /* Free irp */
    IoFreeIrp(Irp);

    /* Done */
    return Status;
}

PVOID
AllocateItem(
    IN POOL_TYPE PoolType,
    IN ULONG ItemSize)
{
    /* Allocate item */
    PVOID Item = ExAllocatePoolWithTag(PoolType, ItemSize, USBCCPG_TAG);

    if (Item)
    {
        /* Zero item */
        RtlZeroMemory(Item, ItemSize);
    }

    /* Return element */
    return Item;
}

VOID
FreeItem(
    IN PVOID Item)
{
    /* Free item */
    ExFreePoolWithTag(Item, USBCCPG_TAG);
}

VOID
DumpFunctionDescriptor(
    IN PUSBC_FUNCTION_DESCRIPTOR FunctionDescriptor,
    IN ULONG FunctionDescriptorCount)
{
    ULONG Index, SubIndex;


    DPRINT1("FunctionCount %lu\n", FunctionDescriptorCount);
    for (Index = 0; Index < FunctionDescriptorCount; Index++)
    {
        DPRINT1("Function %lu\n", Index);
        DPRINT1("FunctionNumber %lu\n", FunctionDescriptor[Index].FunctionNumber);
        DPRINT1("HardwareId %S\n", FunctionDescriptor[Index].HardwareId.Buffer);
        DPRINT1("CompatibleId %S\n", FunctionDescriptor[Index].CompatibleId.Buffer);
        DPRINT1("FunctionDescription %wZ\n", &FunctionDescriptor[Index].FunctionDescription);
        DPRINT1("NumInterfaces %lu\n", FunctionDescriptor[Index].NumberOfInterfaces);

        for(SubIndex = 0; SubIndex < FunctionDescriptor[Index].NumberOfInterfaces; SubIndex++)
        {
            DPRINT1(" Index %lu Interface %p\n", SubIndex, FunctionDescriptor[Index].InterfaceDescriptorList[SubIndex]);
            DPRINT1(" Index %lu Interface InterfaceNumber %x\n", SubIndex, FunctionDescriptor[Index].InterfaceDescriptorList[SubIndex]->bInterfaceNumber);
            DPRINT1(" Index %lu Interface Alternate %x\n", SubIndex, FunctionDescriptor[Index].InterfaceDescriptorList[SubIndex]->bAlternateSetting );
            DPRINT1(" Index %lu bLength %x\n", SubIndex, FunctionDescriptor[Index].InterfaceDescriptorList[SubIndex]->bLength);
            DPRINT1(" Index %lu bDescriptorType %x\n", SubIndex, FunctionDescriptor[Index].InterfaceDescriptorList[SubIndex]->bDescriptorType);
            DPRINT1(" Index %lu bInterfaceNumber %x\n", SubIndex, FunctionDescriptor[Index].InterfaceDescriptorList[SubIndex]->bInterfaceNumber);
            DPRINT1(" Index %lu bAlternateSetting %x\n", SubIndex, FunctionDescriptor[Index].InterfaceDescriptorList[SubIndex]->bAlternateSetting);
            DPRINT1(" Index %lu bNumEndpoints %x\n", SubIndex, FunctionDescriptor[Index].InterfaceDescriptorList[SubIndex]->bNumEndpoints);
            DPRINT1(" Index %lu bInterfaceClass %x\n", SubIndex, FunctionDescriptor[Index].InterfaceDescriptorList[SubIndex]->bInterfaceClass);
            DPRINT1(" Index %lu bInterfaceSubClass %x\n", SubIndex, FunctionDescriptor[Index].InterfaceDescriptorList[SubIndex]->bInterfaceSubClass);
            DPRINT1(" Index %lu bInterfaceProtocol %x\n", SubIndex, FunctionDescriptor[Index].InterfaceDescriptorList[SubIndex]->bInterfaceProtocol);
            DPRINT1(" Index %lu iInterface %x\n", SubIndex, FunctionDescriptor[Index].InterfaceDescriptorList[SubIndex]->iInterface);
        }
    }
}